using Listo.Notification.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Application.Services;

/// <summary>
/// Implements exponential backoff retry logic with jitter to prevent thundering herd.
/// Supports channel-specific retry policies with timeout per attempt.
/// </summary>
public class ExponentialBackoffRetryService
{
    private readonly ILogger<ExponentialBackoffRetryService> _logger;
    private readonly Random _random = new();

    public ExponentialBackoffRetryService(ILogger<ExponentialBackoffRetryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates delay for next retry attempt using exponential backoff with jitter.
    /// Formula: min(baseDelay * (backoffFactor ^ attemptNumber), maxBackoff) + random(0, jitter)
    /// </summary>
    public TimeSpan CalculateDelay(
        int attemptNumber,
        int baseDelaySeconds,
        double backoffFactor,
        int jitterMs)
    {
        // Exponential backoff: baseDelay * (backoffFactor ^ attemptNumber)
        var exponentialDelay = baseDelaySeconds * Math.Pow(backoffFactor, attemptNumber);

        // Add random jitter (0 to jitterMs) to prevent thundering herd
        var jitterSeconds = _random.Next(0, jitterMs) / 1000.0;

        var totalDelay = exponentialDelay + jitterSeconds;

        return TimeSpan.FromSeconds(totalDelay);
    }

    /// <summary>
    /// Executes an operation with retry logic using exponential backoff.
    /// Each attempt has an independent timeout.
    /// </summary>
    public async Task<TResult> ExecuteWithRetryAsync<TResult>(
        Func<Task<TResult>> operation,
        RetryPolicyEntity policy,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < policy.MaxAttempts; attempt++)
        {
            try
            {
                // Create timeout for this specific attempt
                using var timeoutCts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(policy.TimeoutSeconds));

                // Link with parent cancellation token
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                _logger.LogInformation(
                    "Executing operation attempt {Attempt}/{MaxAttempts} with {Timeout}s timeout",
                    attempt + 1, policy.MaxAttempts, policy.TimeoutSeconds);

                // Execute operation with timeout
                var result = await operation().WaitAsync(linkedCts.Token);

                _logger.LogInformation(
                    "Operation succeeded on attempt {Attempt}/{MaxAttempts}",
                    attempt + 1, policy.MaxAttempts);

                return result;
            }
            catch (OperationCanceledException oce) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException(
                    $"Operation timed out after {policy.TimeoutSeconds}s on attempt {attempt + 1}",
                    oce);

                _logger.LogWarning(
                    "Operation timed out on attempt {Attempt}/{MaxAttempts}",
                    attempt + 1, policy.MaxAttempts);
            }
            catch (Exception ex)
            {
                lastException = ex;

                _logger.LogWarning(ex,
                    "Operation failed on attempt {Attempt}/{MaxAttempts}: {Error}",
                    attempt + 1, policy.MaxAttempts, ex.Message);
            }

            // If not the last attempt, calculate delay and retry
            if (attempt < policy.MaxAttempts - 1)
            {
                var delay = CalculateDelay(
                    attempt,
                    policy.BaseDelaySeconds,
                    (double)policy.BackoffFactor,
                    policy.JitterMs);

                _logger.LogInformation(
                    "Retrying after {Delay}ms delay (attempt {Attempt}/{MaxAttempts})",
                    delay.TotalMilliseconds, attempt + 2, policy.MaxAttempts);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // All retries exhausted
        _logger.LogError(lastException,
            "Operation failed after {MaxAttempts} attempts",
            policy.MaxAttempts);

        throw new RetryExhaustedException(
            $"Operation failed after {policy.MaxAttempts} attempts. Last error: {lastException?.Message}",
            lastException);
    }

    /// <summary>
    /// Executes an operation with retry logic, returning success/failure indicator instead of throwing.
    /// Useful for scenarios where you want to handle failure gracefully.
    /// </summary>
    public async Task<RetryResult<TResult>> TryExecuteWithRetryAsync<TResult>(
        Func<Task<TResult>> operation,
        RetryPolicyEntity policy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteWithRetryAsync(operation, policy, cancellationToken);
            return RetryResult<TResult>.Success(result);
        }
        catch (RetryExhaustedException ex)
        {
            return RetryResult<TResult>.Failure(ex.InnerException ?? ex);
        }
    }
}

/// <summary>
/// Exception thrown when all retry attempts are exhausted.
/// </summary>
public class RetryExhaustedException : Exception
{
    public RetryExhaustedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Result of a retry operation indicating success or failure.
/// </summary>
public record RetryResult<TResult>
{
    public bool IsSuccess { get; init; }
    public TResult? Result { get; init; }
    public Exception? Error { get; init; }

    public static RetryResult<TResult> Success(TResult result) =>
        new() { IsSuccess = true, Result = result };

    public static RetryResult<TResult> Failure(Exception error) =>
        new() { IsSuccess = false, Error = error };
}

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Configurable retry policies per service origin and channel.
/// Supports exponential backoff with jitter for reliable notification delivery.
/// </summary>
public class RetryPolicyEntity
{
    /// <summary>
    /// Unique policy identifier
    /// </summary>
    public Guid PolicyId { get; set; }

    /// <summary>
    /// Service origin (auth, orders, ridesharing, products, * for wildcard)
    /// </summary>
    public string ServiceOrigin { get; set; } = string.Empty;

    /// <summary>
    /// Channel (push, sms, email, inApp, * for wildcard)
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Maximum retry attempts (default: 6)
    /// </summary>
    public int MaxAttempts { get; set; } = 6;

    /// <summary>
    /// Initial retry delay in seconds (default: 5)
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Exponential backoff multiplier (default: 2.0)
    /// </summary>
    public decimal BackoffFactor { get; set; } = 2.0m;

    /// <summary>
    /// Random jitter in milliseconds to prevent thundering herd (default: 1000)
    /// </summary>
    public int JitterMs { get; set; } = 1000;

    /// <summary>
    /// Per-attempt timeout in seconds (default: 30)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Is this policy active (default: true)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Calculate next attempt delay using exponential backoff with jitter.
    /// Formula: (BaseDelaySeconds * (BackoffFactor ^ Attempts)) + Random(0, JitterMs)
    /// </summary>
    public TimeSpan CalculateNextDelay(int attemptNumber)
    {
        if (attemptNumber >= MaxAttempts)
            throw new InvalidOperationException($"Attempt {attemptNumber} exceeds maximum attempts {MaxAttempts}");

        var baseDelay = BaseDelaySeconds * Math.Pow((double)BackoffFactor, attemptNumber);
        var jitter = Random.Shared.Next(0, JitterMs);
        var totalDelayMs = (baseDelay * 1000) + jitter;

        return TimeSpan.FromMilliseconds(totalDelayMs);
    }
}

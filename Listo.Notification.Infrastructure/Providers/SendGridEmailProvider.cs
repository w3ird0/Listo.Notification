using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Listo.Notification.Infrastructure.Providers;

/// <summary>
/// SendGrid email provider implementation with circuit breaker pattern.
/// </summary>
public class SendGridEmailProvider : IEmailProvider
{
    private readonly ILogger<SendGridEmailProvider> _logger;
    private readonly SendGridOptions _options;
    private readonly ISendGridClient _client;
    private int _consecutiveFailures;
    private DateTime? _circuitBreakerOpenUntil;
    private readonly object _circuitLock = new();

    public NotificationChannel Channel => NotificationChannel.Email;
    public string ProviderName => "SendGrid";
    public bool IsFallback => false;

    public SendGridEmailProvider(
        ILogger<SendGridEmailProvider> logger,
        IOptions<SendGridOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _client = new SendGridClient(_options.ApiKey);
    }

    public async Task<DeliveryResult> SendAsync(DeliveryRequest request, CancellationToken cancellationToken = default)
    {
        // Check circuit breaker
        if (IsCircuitOpen())
        {
            _logger.LogWarning("Circuit breaker is OPEN for SendGrid email provider");
            return new DeliveryResult(
                Success: false,
                ProviderId: ProviderName,
                ProviderMessageId: null,
                ErrorMessage: "Circuit breaker is open - provider temporarily unavailable");
        }

        try
        {
            var from = new EmailAddress(_options.FromEmail, _options.FromName);
            var to = new EmailAddress(request.Recipient);
            var msg = MailHelper.CreateSingleEmail(
                from,
                to,
                request.Subject,
                plainTextContent: request.Body,
                htmlContent: request.Body);

            var response = await _client.SendEmailAsync(msg, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure();
                var errorBody = await response.Body.ReadAsStringAsync(cancellationToken);
                
                _logger.LogError(
                    "SendGrid API error: StatusCode={StatusCode}, Body={ErrorBody}",
                    response.StatusCode, errorBody);

                return new DeliveryResult(
                    Success: false,
                    ProviderId: ProviderName,
                    ProviderMessageId: null,
                    ErrorMessage: $"SendGrid error {response.StatusCode}: {errorBody}");
            }

            RecordSuccess();
            
            // Extract message ID from response headers
            var messageId = response.Headers?.GetValues("X-Message-Id")?.FirstOrDefault();
            
            _logger.LogInformation(
                "Email sent via SendGrid: NotificationId={NotificationId}, MessageId={MessageId}",
                request.NotificationId, messageId);

            return new DeliveryResult(
                Success: true,
                ProviderId: ProviderName,
                ProviderMessageId: messageId,
                ProviderMetadata: new Dictionary<string, string>
                {
                    ["statusCode"] = ((int)response.StatusCode).ToString()
                });
        }
        catch (Exception ex)
        {
            RecordFailure();
            _logger.LogError(ex,
                "Error sending email via SendGrid: NotificationId={NotificationId}",
                request.NotificationId);

            return new DeliveryResult(
                Success: false,
                ProviderId: ProviderName,
                ProviderMessageId: null,
                ErrorMessage: $"SendGrid exception: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // SendGrid doesn't have a dedicated health check endpoint
            // We'll just verify we can construct a client
            return _client != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendGrid health check failed");
            return false;
        }
    }

    private bool IsCircuitOpen()
    {
        lock (_circuitLock)
        {
            if (_circuitBreakerOpenUntil.HasValue && DateTime.UtcNow < _circuitBreakerOpenUntil.Value)
            {
                return true;
            }

            // Circuit can be closed again
            if (_circuitBreakerOpenUntil.HasValue && DateTime.UtcNow >= _circuitBreakerOpenUntil.Value)
            {
                _logger.LogInformation("Circuit breaker closing - attempting to reset SendGrid provider");
                _circuitBreakerOpenUntil = null;
                _consecutiveFailures = 0;
            }

            return false;
        }
    }

    private void RecordFailure()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures++;

            // Open circuit after 5 consecutive failures
            if (_consecutiveFailures >= 5 && !_circuitBreakerOpenUntil.HasValue)
            {
                _circuitBreakerOpenUntil = DateTime.UtcNow.AddSeconds(60);
                _logger.LogWarning(
                    "Circuit breaker OPENED for SendGrid email provider after {Failures} consecutive failures. Will retry at {RetryTime}",
                    _consecutiveFailures, _circuitBreakerOpenUntil.Value);
            }
        }
    }

    private void RecordSuccess()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures = 0;
            _circuitBreakerOpenUntil = null;
        }
    }
}

/// <summary>
/// Configuration options for SendGrid email provider.
/// </summary>
public class SendGridOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}

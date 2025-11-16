using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using TwilioAccount = Twilio.Rest.Api.V2010.AccountResource;

namespace Listo.Notification.Infrastructure.Providers;

/// <summary>
/// Twilio SMS provider implementation with circuit breaker pattern.
/// </summary>
public class TwilioSmsProvider : ISmsProvider
{
    private readonly ILogger<TwilioSmsProvider> _logger;
    private readonly TwilioOptions _options;
    private int _consecutiveFailures;
    private DateTime? _circuitBreakerOpenUntil;
    private readonly object _circuitLock = new();

    public NotificationChannel Channel => NotificationChannel.Sms;
    public string ProviderName => "Twilio";
    public bool IsFallback => false;

    public TwilioSmsProvider(
        ILogger<TwilioSmsProvider> logger,
        IOptions<TwilioOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        // Initialize Twilio client
        TwilioClient.Init(_options.AccountSid, _options.AuthToken);
    }

    public async Task<DeliveryResult> SendAsync(DeliveryRequest request, CancellationToken cancellationToken = default)
    {
        // Check circuit breaker
        if (IsCircuitOpen())
        {
            _logger.LogWarning("Circuit breaker is OPEN for Twilio SMS provider");
            return new DeliveryResult(
                Success: false,
                ProviderId: ProviderName,
                ProviderMessageId: null,
                ErrorMessage: "Circuit breaker is open - provider temporarily unavailable");
        }

        try
        {
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(request.Recipient),
                from: new PhoneNumber(_options.FromPhoneNumber),
                body: request.Body);

            if (message.ErrorCode.HasValue)
            {
                RecordFailure();
                return new DeliveryResult(
                    Success: false,
                    ProviderId: ProviderName,
                    ProviderMessageId: message.Sid,
                    ErrorMessage: $"Twilio error {message.ErrorCode}: {message.ErrorMessage}");
            }

            RecordSuccess();
            _logger.LogInformation(
                "SMS sent via Twilio: NotificationId={NotificationId}, MessageSid={MessageSid}, Status={Status}",
                request.NotificationId, message.Sid, message.Status);

            return new DeliveryResult(
                Success: true,
                ProviderId: ProviderName,
                ProviderMessageId: message.Sid,
                ProviderMetadata: new Dictionary<string, string>
                {
                    ["status"] = message.Status.ToString(),
                    ["price"] = message.Price ?? "0",
                    ["priceUnit"] = message.PriceUnit ?? "USD"
                });
        }
        catch (Exception ex)
        {
            RecordFailure();
            _logger.LogError(ex, 
                "Error sending SMS via Twilio: NotificationId={NotificationId}",
                request.NotificationId);

            return new DeliveryResult(
                Success: false,
                ProviderId: ProviderName,
                ProviderMessageId: null,
                ErrorMessage: $"Twilio exception: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple health check by fetching account info
            var account = await TwilioAccount.FetchAsync(pathSid: _options.AccountSid);
            return account != null && account.Status == TwilioAccount.StatusEnum.Active;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Twilio health check failed");
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
                _logger.LogInformation("Circuit breaker closing - attempting to reset Twilio provider");
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
                    "Circuit breaker OPENED for Twilio SMS provider after {Failures} consecutive failures. Will retry at {RetryTime}",
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
/// Configuration options for Twilio SMS provider.
/// </summary>
public class TwilioOptions
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromPhoneNumber { get; set; } = string.Empty;
}

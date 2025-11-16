using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Enums;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Listo.Notification.Infrastructure.Providers;

/// <summary>
/// Firebase Cloud Messaging (FCM) push notification provider with circuit breaker pattern.
/// </summary>
public class FcmPushProvider : IPushProvider
{
    private readonly ILogger<FcmPushProvider> _logger;
    private readonly FcmOptions _options;
    private readonly HttpClient _httpClient;
    private int _consecutiveFailures;
    private DateTime? _circuitBreakerOpenUntil;
    private readonly object _circuitLock = new();

    public NotificationChannel Channel => NotificationChannel.Push;
    public string ProviderName => "FCM";
    public bool IsFallback => false;

    public FcmPushProvider(
        ILogger<FcmPushProvider> logger,
        IOptions<FcmOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClientFactory.CreateClient("FCM");
        
        // Configure HTTP client for FCM
        _httpClient.BaseAddress = new Uri("https://fcm.googleapis.com/v1/projects/");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ServerKey}");
    }

    public async Task<DeliveryResult> SendAsync(DeliveryRequest request, CancellationToken cancellationToken = default)
    {
        // Check circuit breaker
        if (IsCircuitOpen())
        {
            _logger.LogWarning("Circuit breaker is OPEN for FCM push provider");
            return new DeliveryResult(
                Success: false,
                ProviderId: ProviderName,
                ProviderMessageId: null,
                ErrorMessage: "Circuit breaker is open - provider temporarily unavailable");
        }

        try
        {
            // Build FCM message payload
            var payload = new
            {
                message = new
                {
                    token = request.Recipient, // FCM device token
                    notification = new
                    {
                        title = request.Subject ?? "Notification",
                        body = request.Body
                    },
                    data = request.Metadata ?? new Dictionary<string, string>(),
                    android = new
                    {
                        priority = "normal"
                    },
                    apns = new
                    {
                        headers = new
                        {
                            apns_priority = "5"
                        }
                    }
                }
            };

            var endpoint = $"{_options.ProjectId}/messages:send";
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure();
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _logger.LogError(
                    "FCM API error: StatusCode={StatusCode}, Body={ErrorBody}",
                    response.StatusCode, errorBody);

                return new DeliveryResult(
                    Success: false,
                    ProviderId: ProviderName,
                    ProviderMessageId: null,
                    ErrorMessage: $"FCM error {response.StatusCode}: {errorBody}");
            }

            RecordSuccess();
            
            // Parse response to get message ID
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var messageId = responseJson.GetProperty("name").GetString();
            
            _logger.LogInformation(
                "Push notification sent via FCM: NotificationId={NotificationId}, MessageId={MessageId}",
                request.NotificationId, messageId);

            return new DeliveryResult(
                Success: true,
                ProviderId: ProviderName,
                ProviderMessageId: messageId,
                ProviderMetadata: new Dictionary<string, string>
                {
                    ["statusCode"] = ((int)response.StatusCode).ToString(),
                    ["platform"] = "FCM"
                });
        }
        catch (Exception ex)
        {
            RecordFailure();
            _logger.LogError(ex,
                "Error sending push notification via FCM: NotificationId={NotificationId}",
                request.NotificationId);

            return new DeliveryResult(
                Success: false,
                ProviderId: ProviderName,
                ProviderMessageId: null,
                ErrorMessage: $"FCM exception: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple health check - verify we can reach FCM endpoint
            var response = await _httpClient.GetAsync(
                $"{_options.ProjectId}",
                cancellationToken);
            
            // Even a 404 or 401 means the service is reachable
            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM health check failed");
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
                _logger.LogInformation("Circuit breaker closing - attempting to reset FCM provider");
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
                    "Circuit breaker OPENED for FCM push provider after {Failures} consecutive failures. Will retry at {RetryTime}",
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
/// Configuration options for FCM push provider.
/// </summary>
public class FcmOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string ServerKey { get; set; } = string.Empty;
}

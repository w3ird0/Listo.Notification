using System.Text.Json;
using System.Text.Json.Serialization;
using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Listo.Notification.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for communicating with Listo.Auth service.
/// Provides device token lookup for push notification delivery.
/// </summary>
public class AuthServiceClient : IAuthServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthServiceOptions _options;
    private readonly ILogger<AuthServiceClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AuthServiceClient(
        HttpClient httpClient,
        IOptions<AuthServiceOptions> options,
        ILogger<AuthServiceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<DeviceTokenDto>> GetUserDeviceTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Fetching device tokens for user {UserId} from Listo.Auth service",
                userId);

            // NOTE: Auth service currently has GET /api/v1/users/devices (authenticated user only)
            // We need a service-to-service endpoint: GET /api/v1/internal/users/{userId}/devices
            // Or a query parameter approach: GET /api/v1/internal/devices?userId={userId}
            // For now, using /{userId}/devices as the most RESTful approach
            var requestUri = $"/api/v1/internal/users/{userId}/devices";

            var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch device tokens from Auth service. StatusCode={StatusCode}, UserId={UserId}",
                    response.StatusCode, userId);

                return new List<DeviceTokenDto>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var deviceListResponse = JsonSerializer.Deserialize<AuthDeviceListResponse>(content, JsonOptions);

            if (deviceListResponse?.Devices == null || deviceListResponse.Devices.Count == 0)
            {
                _logger.LogInformation(
                    "No devices found for user {UserId}",
                    userId);

                return new List<DeviceTokenDto>();
            }

            // Filter to active devices with push tokens enabled
            var deviceTokens = deviceListResponse.Devices
                .Where(d => d.IsActive && d.PushNotificationsEnabled && !string.IsNullOrWhiteSpace(d.PushToken))
                .Select(d => new DeviceTokenDto
                {
                    DeviceId = d.DeviceId,
                    Platform = d.Platform ?? "Unknown",
                    PushToken = d.PushToken!,
                    IsActive = d.IsActive
                })
                .ToList();

            _logger.LogInformation(
                "Found {Count} active devices with push tokens for user {UserId}",
                deviceTokens.Count, userId);

            return deviceTokens;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request failed while fetching device tokens for user {UserId}",
                userId);
            return new List<DeviceTokenDto>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JSON deserialization failed while parsing device tokens for user {UserId}",
                userId);
            return new List<DeviceTokenDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while fetching device tokens for user {UserId}",
                userId);
            return new List<DeviceTokenDto>();
        }
    }

    #region Internal Deserialization Models

    /// <summary>
    /// Internal model for deserializing Auth service device list response.
    /// Maps to Listo.Auth DeviceListResponse structure.
    /// </summary>
    internal class AuthDeviceListResponse
    {
        [JsonPropertyName("devices")]
        public List<AuthDeviceResponse> Devices { get; set; } = new();

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Internal model for deserializing Auth service device response.
    /// NOTE: Auth service must include pushToken in response for this integration to work.
    /// </summary>
    internal class AuthDeviceResponse
    {
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("pushNotificationsEnabled")]
        public bool PushNotificationsEnabled { get; set; }

        /// <summary>
        /// FCM/APNS push token. Required for push notifications.
        /// NOTE: This field must be added to Auth service DeviceResponse if not present.
        /// </summary>
        [JsonPropertyName("pushToken")]
        public string? PushToken { get; set; }
    }

    #endregion
}

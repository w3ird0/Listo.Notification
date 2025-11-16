using System.Text.Json.Serialization;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Device token information from Listo.Auth service.
/// Used for push notification delivery.
/// </summary>
public record DeviceTokenDto
{
    /// <summary>
    /// Unique device identifier from Auth service.
    /// </summary>
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; init; }

    /// <summary>
    /// Device platform (iOS, Android, Web).
    /// </summary>
    [JsonPropertyName("platform")]
    public required string Platform { get; init; }

    /// <summary>
    /// FCM/APNS push notification token.
    /// </summary>
    [JsonPropertyName("pushToken")]
    public required string PushToken { get; init; }

    /// <summary>
    /// Whether the device is currently active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; } = true;
}

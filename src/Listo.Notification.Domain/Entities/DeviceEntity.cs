namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents a user device registered for push notifications.
/// </summary>
public class DeviceEntity
{
    public Guid DeviceId { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// FCM/APNS token (hashed for security)
    /// </summary>
    public string DeviceToken { get; set; } = string.Empty;

    /// <summary>
    /// Platform: android, ios, web
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// JSON: model, osVersion, appVersion
    /// </summary>
    public string? DeviceInfo { get; set; }

    public DateTime LastSeen { get; set; }

    public bool Active { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

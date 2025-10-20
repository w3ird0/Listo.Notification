namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents the platform of a device for push notifications.
/// </summary>
public enum DevicePlatform
{
    /// <summary>
    /// Android device using FCM
    /// </summary>
    Android = 1,

    /// <summary>
    /// iOS device using APNs
    /// </summary>
    iOS = 2,

    /// <summary>
    /// Web browser using Web Push API
    /// </summary>
    Web = 3
}

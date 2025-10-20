namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents the current status of a notification in its delivery lifecycle.
/// </summary>
public enum NotificationStatus
{
    /// <summary>
    /// Notification is queued for delivery
    /// </summary>
    Queued = 1,

    /// <summary>
    /// Notification has been sent to the provider
    /// </summary>
    Sent = 2,

    /// <summary>
    /// Notification was successfully delivered to the recipient
    /// </summary>
    Delivered = 3,

    /// <summary>
    /// Notification was opened/read by the recipient
    /// </summary>
    Opened = 4,

    /// <summary>
    /// Notification delivery failed after all retry attempts
    /// </summary>
    Failed = 5
}

namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents the priority level of a notification for processing and delivery.
/// </summary>
public enum Priority
{
    /// <summary>
    /// Low priority - can be delayed if necessary
    /// </summary>
    Low = 1,

    /// <summary>
    /// Normal priority - standard delivery
    /// </summary>
    Normal = 2,

    /// <summary>
    /// High priority - immediate delivery required (e.g., driver assignment, OTP)
    /// </summary>
    High = 3
}

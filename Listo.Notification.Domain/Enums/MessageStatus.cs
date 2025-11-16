namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents the delivery status of a message in a conversation.
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// Message has been sent
    /// </summary>
    Sent = 1,

    /// <summary>
    /// Message was delivered to recipient
    /// </summary>
    Delivered = 2,

    /// <summary>
    /// Message was read by recipient
    /// </summary>
    Read = 3,

    /// <summary>
    /// Message delivery failed
    /// </summary>
    Failed = 4
}

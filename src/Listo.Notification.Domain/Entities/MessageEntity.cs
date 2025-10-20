namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents an individual message within a conversation.
/// </summary>
public class MessageEntity
{
    public Guid MessageId { get; set; }

    public Guid ConversationId { get; set; }

    public string SenderUserId { get; set; } = string.Empty;

    /// <summary>
    /// Specific recipient (null for all participants)
    /// </summary>
    public string? RecipientUserId { get; set; }

    /// <summary>
    /// Message content (text, markdown)
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of file URLs
    /// </summary>
    public string? AttachmentsJson { get; set; }

    /// <summary>
    /// Status: sent, delivered, read, failed
    /// </summary>
    public string Status { get; set; } = "sent";

    public DateTime SentAt { get; set; }

    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Navigation property to parent conversation
    /// </summary>
    public ConversationEntity? Conversation { get; set; }
}

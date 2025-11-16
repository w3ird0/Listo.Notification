using Listo.Notification.Domain.Enums;

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
    public MessageStatus Status { get; set; } = MessageStatus.Sent;

    public DateTime SentAt { get; set; }

    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Soft delete flag for data retention compliance.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Timestamp when the message was soft deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Navigation property to parent conversation
    /// </summary>
    public ConversationEntity? Conversation { get; set; }
}

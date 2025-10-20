using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents an in-app messaging conversation (Customer↔Support, Customer↔Driver).
/// </summary>
public class ConversationEntity
{
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Type: customer_support, customer_driver
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of participant user IDs
    /// </summary>
    public string ParticipantsJson { get; set; } = "[]";

    public ServiceOrigin ServiceOrigin { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Navigation property for messages in this conversation
    /// </summary>
    public ICollection<MessageEntity> Messages { get; set; } = new List<MessageEntity>();
}

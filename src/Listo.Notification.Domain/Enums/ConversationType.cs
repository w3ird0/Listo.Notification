namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents the type of in-app messaging conversation.
/// </summary>
public enum ConversationType
{
    /// <summary>
    /// Conversation between customer and support
    /// </summary>
    CustomerSupport = 1,

    /// <summary>
    /// Conversation between customer and driver
    /// </summary>
    CustomerDriver = 2
}

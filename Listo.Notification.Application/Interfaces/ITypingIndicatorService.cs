namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Service for managing typing indicators with Redis TTL (10 seconds).
/// Handles reconnection scenarios by persisting state temporarily.
/// </summary>
public interface ITypingIndicatorService
{
    /// <summary>
    /// Marks a user as typing in a conversation.
    /// Stores state in Redis with 10-second TTL.
    /// </summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetTypingAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears typing indicator for a user in a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearTypingAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users currently typing in a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of user IDs currently typing.</returns>
    Task<IEnumerable<string>> GetTypingUsersAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific user is typing in a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user is currently typing, false otherwise.</returns>
    Task<bool> IsUserTypingAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default);
}

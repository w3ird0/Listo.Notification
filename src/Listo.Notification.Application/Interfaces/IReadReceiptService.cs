namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Service for managing read receipts with dual storage (database + Redis cache).
/// </summary>
public interface IReadReceiptService
{
    /// <summary>
    /// Records that a message has been read by a user.
    /// Updates database and stores in Redis with 30-day TTL.
    /// </summary>
    /// <param name="messageId">Message identifier.</param>
    /// <param name="userId">User who read the message.</param>
    /// <param name="readAt">Timestamp when message was read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordReadReceiptAsync(
        Guid messageId,
        string userId,
        DateTime readAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets read receipt information for a message from Redis (fast path).
    /// Falls back to database if not in cache.
    /// </summary>
    /// <param name="messageId">Message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read receipt information or null if not read.</returns>
    Task<ReadReceipt?> GetReadReceiptAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets read receipts for multiple messages efficiently (batch operation).
    /// </summary>
    /// <param name="messageIds">Collection of message identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of messageId to read receipt.</returns>
    Task<Dictionary<Guid, ReadReceipt>> GetBatchReadReceiptsAsync(
        IEnumerable<Guid> messageIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages in a conversation that have been read by a specific user.
    /// </summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of message IDs that have been read.</returns>
    Task<IEnumerable<Guid>> GetReadMessageIdsAsync(
        Guid conversationId,
        string userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a read receipt for a message.
/// </summary>
public record ReadReceipt(
    Guid MessageId,
    string UserId,
    DateTime ReadAt);

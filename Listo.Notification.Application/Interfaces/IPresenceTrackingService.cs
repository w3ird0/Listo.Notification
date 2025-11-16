namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Service for tracking user presence (online/offline status) with Redis TTL.
/// </summary>
public interface IPresenceTrackingService
{
    /// <summary>
    /// Marks a user as online and stores presence in Redis with TTL (5 minutes).
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="connectionId">SignalR connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetUserOnlineAsync(string userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the user's presence TTL in Redis (extends by 5 minutes).
    /// Called on user activity to keep presence alive.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshPresenceAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a user as offline and records last seen timestamp in Redis.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="connectionId">SignalR connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetUserOfflineAsync(string userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current presence status of a user.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User presence information (online, last seen timestamp).</returns>
    Task<UserPresence> GetUserPresenceAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last seen timestamp for an offline user.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Last seen timestamp in UTC, or null if never seen.</returns>
    Task<DateTime?> GetLastSeenAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets presence information for multiple users efficiently.
    /// </summary>
    /// <param name="userIds">Collection of user identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of userId to presence information.</returns>
    Task<Dictionary<string, UserPresence>> GetBatchPresenceAsync(
        IEnumerable<string> userIds, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a user's presence information.
/// </summary>
public record UserPresence(
    string UserId,
    bool IsOnline,
    DateTime? LastSeen,
    int ActiveConnectionCount);

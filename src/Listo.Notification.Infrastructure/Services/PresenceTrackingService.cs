using Listo.Notification.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Listo.Notification.Infrastructure.Services;

/// <summary>
/// Redis-based implementation of presence tracking service.
/// Stores user presence with 5-minute TTL and tracks active connections.
/// </summary>
public class PresenceTrackingService : IPresenceTrackingService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<PresenceTrackingService> _logger;
    private static readonly TimeSpan PresenceTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LastSeenTtl = TimeSpan.FromDays(30);

    // Redis key patterns
    private const string PresenceKeyPrefix = "presence:user:";
    private const string ConnectionsKeyPrefix = "presence:connections:";
    private const string LastSeenKeyPrefix = "presence:lastseen:";

    public PresenceTrackingService(
        IConnectionMultiplexer redis,
        ILogger<PresenceTrackingService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SetUserOnlineAsync(string userId, string connectionId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var presenceKey = $"{PresenceKeyPrefix}{userId}";
        var connectionsKey = $"{ConnectionsKeyPrefix}{userId}";

        try
        {
            var now = DateTime.UtcNow;
            var batch = db.CreateBatch();

            // Mark user as online with TTL
            var setPresenceTask = batch.StringSetAsync(presenceKey, now.ToString("O"), PresenceTtl);

            // Add connection to set (supports multiple connections per user)
            var addConnectionTask = batch.SetAddAsync(connectionsKey, connectionId);
            var expireConnectionsTask = batch.KeyExpireAsync(connectionsKey, PresenceTtl);

            batch.Execute();

            await Task.WhenAll(setPresenceTask, addConnectionTask, expireConnectionsTask);

            _logger.LogInformation(
                "User marked as online: UserId={UserId}, ConnectionId={ConnectionId}",
                userId, connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to set user online: UserId={UserId}, ConnectionId={ConnectionId}",
                userId, connectionId);
            throw;
        }
    }

    public async Task RefreshPresenceAsync(string userId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var presenceKey = $"{PresenceKeyPrefix}{userId}";
        var connectionsKey = $"{ConnectionsKeyPrefix}{userId}";

        try
        {
            var now = DateTime.UtcNow;
            var batch = db.CreateBatch();

            // Refresh presence TTL
            var setPresenceTask = batch.StringSetAsync(presenceKey, now.ToString("O"), PresenceTtl);
            var expireConnectionsTask = batch.KeyExpireAsync(connectionsKey, PresenceTtl);

            batch.Execute();
            await Task.WhenAll(setPresenceTask, expireConnectionsTask);

            _logger.LogDebug("Presence refreshed for UserId={UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh presence for UserId={UserId}", userId);
            // Non-critical operation, don't throw
        }
    }

    public async Task SetUserOfflineAsync(string userId, string connectionId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var connectionsKey = $"{ConnectionsKeyPrefix}{userId}";
        var presenceKey = $"{PresenceKeyPrefix}{userId}";
        var lastSeenKey = $"{LastSeenKeyPrefix}{userId}";

        try
        {
            // Remove connection from set
            await db.SetRemoveAsync(connectionsKey, connectionId);

            // Check if user has other active connections
            var remainingConnections = await db.SetLengthAsync(connectionsKey);

            if (remainingConnections == 0)
            {
                // No more connections - mark as offline
                var now = DateTime.UtcNow;
                var batch = db.CreateBatch();

                var deletePresenceTask = batch.KeyDeleteAsync(presenceKey);
                var setLastSeenTask = batch.StringSetAsync(lastSeenKey, now.ToString("O"), LastSeenTtl);
                var deleteConnectionsTask = batch.KeyDeleteAsync(connectionsKey);

                batch.Execute();
                await Task.WhenAll(deletePresenceTask, setLastSeenTask, deleteConnectionsTask);

                _logger.LogInformation(
                    "User marked as offline: UserId={UserId}, LastSeen={LastSeen}",
                    userId, now);
            }
            else
            {
                _logger.LogDebug(
                    "Connection removed, user still online: UserId={UserId}, RemainingConnections={Count}",
                    userId, remainingConnections);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to set user offline: UserId={UserId}, ConnectionId={ConnectionId}",
                userId, connectionId);
            throw;
        }
    }

    public async Task<UserPresence> GetUserPresenceAsync(string userId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var presenceKey = $"{PresenceKeyPrefix}{userId}";
        var connectionsKey = $"{ConnectionsKeyPrefix}{userId}";
        var lastSeenKey = $"{LastSeenKeyPrefix}{userId}";

        try
        {
            var presenceValue = await db.StringGetAsync(presenceKey);
            var connectionCount = (int)await db.SetLengthAsync(connectionsKey);

            if (!presenceValue.IsNullOrEmpty && DateTime.TryParse(presenceValue, out var lastActivity))
            {
                // User is online
                return new UserPresence(userId, true, lastActivity, connectionCount);
            }

            // User is offline, check last seen
            var lastSeenValue = await db.StringGetAsync(lastSeenKey);
            DateTime? lastSeen = null;

            if (!lastSeenValue.IsNullOrEmpty && DateTime.TryParse(lastSeenValue, out var lastSeenTime))
            {
                lastSeen = lastSeenTime;
            }

            return new UserPresence(userId, false, lastSeen, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get presence for UserId={UserId}", userId);
            throw;
        }
    }

    public async Task<DateTime?> GetLastSeenAsync(string userId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var lastSeenKey = $"{LastSeenKeyPrefix}{userId}";

        try
        {
            var lastSeenValue = await db.StringGetAsync(lastSeenKey);

            if (!lastSeenValue.IsNullOrEmpty && DateTime.TryParse(lastSeenValue, out var lastSeen))
            {
                return lastSeen;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get last seen for UserId={UserId}", userId);
            return null;
        }
    }

    public async Task<Dictionary<string, UserPresence>> GetBatchPresenceAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var result = new Dictionary<string, UserPresence>();

        try
        {
            var batch = db.CreateBatch();
            var tasks = new List<Task<(string UserId, RedisValue Presence, long Connections, RedisValue LastSeen)>>();

            foreach (var userId in userIds)
            {
                var presenceKey = $"{PresenceKeyPrefix}{userId}";
                var connectionsKey = $"{ConnectionsKeyPrefix}{userId}";
                var lastSeenKey = $"{LastSeenKeyPrefix}{userId}";

                var presenceTask = batch.StringGetAsync(presenceKey);
                var connectionsTask = batch.SetLengthAsync(connectionsKey);
                var lastSeenTask = batch.StringGetAsync(lastSeenKey);

                tasks.Add(Task.WhenAll(presenceTask, connectionsTask, lastSeenTask)
                    .ContinueWith(t => (userId, presenceTask.Result, connectionsTask.Result, lastSeenTask.Result)));
            }

            batch.Execute();
            await Task.WhenAll(tasks);

            foreach (var task in tasks)
            {
                var (userId, presence, connections, lastSeenValue) = task.Result;
                bool isOnline = !presence.IsNullOrEmpty;
                DateTime? lastSeen = null;

                if (!presence.IsNullOrEmpty && DateTime.TryParse(presence, out var presenceTime))
                {
                    lastSeen = presenceTime;
                }
                else if (!lastSeenValue.IsNullOrEmpty && DateTime.TryParse(lastSeenValue, out var lastSeenTime))
                {
                    lastSeen = lastSeenTime;
                }

                result[userId] = new UserPresence(userId, isOnline, lastSeen, (int)connections);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get batch presence for {Count} users", userIds.Count());
            throw;
        }
    }
}

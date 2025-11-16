using Listo.Notification.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Listo.Notification.Infrastructure.Services;

/// <summary>
/// Redis-based implementation of typing indicator service with 10-second TTL.
/// Persists typing state temporarily to handle reconnection scenarios.
/// </summary>
public class TypingIndicatorService : ITypingIndicatorService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<TypingIndicatorService> _logger;
    private static readonly TimeSpan TypingTtl = TimeSpan.FromSeconds(10);
    private const string TypingKeyPrefix = "typing:conversation:";

    public TypingIndicatorService(
        IConnectionMultiplexer redis,
        ILogger<TypingIndicatorService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SetTypingAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{TypingKeyPrefix}{conversationId}";

        try
        {
            // Add user to typing set with TTL
            await db.SetAddAsync(key, userId);
            await db.KeyExpireAsync(key, TypingTtl);

            _logger.LogDebug(
                "User started typing: ConversationId={ConversationId}, UserId={UserId}",
                conversationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to set typing indicator: ConversationId={ConversationId}, UserId={UserId}",
                conversationId, userId);
            // Non-critical operation, don't throw
        }
    }

    public async Task ClearTypingAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{TypingKeyPrefix}{conversationId}";

        try
        {
            await db.SetRemoveAsync(key, userId);

            _logger.LogDebug(
                "User stopped typing: ConversationId={ConversationId}, UserId={UserId}",
                conversationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to clear typing indicator: ConversationId={ConversationId}, UserId={UserId}",
                conversationId, userId);
            // Non-critical operation, don't throw
        }
    }

    public async Task<IEnumerable<string>> GetTypingUsersAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{TypingKeyPrefix}{conversationId}";

        try
        {
            var members = await db.SetMembersAsync(key);
            return members.Select(m => m.ToString()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get typing users: ConversationId={ConversationId}",
                conversationId);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> IsUserTypingAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{TypingKeyPrefix}{conversationId}";

        try
        {
            return await db.SetContainsAsync(key, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to check if user is typing: ConversationId={ConversationId}, UserId={UserId}",
                conversationId, userId);
            return false;
        }
    }
}

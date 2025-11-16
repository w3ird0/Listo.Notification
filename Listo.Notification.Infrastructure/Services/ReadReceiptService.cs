using Listo.Notification.Application.Interfaces;
using Listo.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Listo.Notification.Infrastructure.Services;

/// <summary>
/// Implements read receipt management with dual storage (database + Redis cache).
/// Redis provides fast queries with 30-day TTL, database provides durable storage.
/// </summary>
public class ReadReceiptService : IReadReceiptService
{
    private readonly NotificationDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ReadReceiptService> _logger;
    private static readonly TimeSpan ReadReceiptTtl = TimeSpan.FromDays(30);
    private const string ReadReceiptKeyPrefix = "readreceipt:message:";
    private const string ConversationReadKeyPrefix = "readreceipt:conversation:";

    public ReadReceiptService(
        NotificationDbContext dbContext,
        IConnectionMultiplexer redis,
        ILogger<ReadReceiptService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RecordReadReceiptAsync(
        Guid messageId,
        string userId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Update database
            var message = await _dbContext.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message == null)
            {
                _logger.LogWarning("Cannot record read receipt: Message not found. MessageId={MessageId}", messageId);
                return;
            }

            message.ReadAt = readAt;
            message.Status = Listo.Notification.Domain.Enums.MessageStatus.Read;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 2. Store in Redis with 30-day TTL
            var db = _redis.GetDatabase();
            var key = $"{ReadReceiptKeyPrefix}{messageId}";
            var receipt = new ReadReceipt(messageId, userId, readAt);
            var json = JsonSerializer.Serialize(receipt);

            await db.StringSetAsync(key, json, ReadReceiptTtl);

            // 3. Add to conversation read set
            var conversationKey = $"{ConversationReadKeyPrefix}{message.ConversationId}:{userId}";
            await db.SetAddAsync(conversationKey, messageId.ToString());
            await db.KeyExpireAsync(conversationKey, ReadReceiptTtl);

            _logger.LogInformation(
                "Read receipt recorded: MessageId={MessageId}, UserId={UserId}, ReadAt={ReadAt}",
                messageId, userId, readAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to record read receipt: MessageId={MessageId}, UserId={UserId}",
                messageId, userId);
            throw;
        }
    }

    public async Task<ReadReceipt?> GetReadReceiptAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Fast path: Check Redis first
            var db = _redis.GetDatabase();
            var key = $"{ReadReceiptKeyPrefix}{messageId}";
            var cached = await db.StringGetAsync(key);

            if (!cached.IsNullOrEmpty)
            {
                return JsonSerializer.Deserialize<ReadReceipt>(cached!);
            }

            // Slow path: Query database
            var message = await _dbContext.Messages
                .Where(m => m.MessageId == messageId && m.ReadAt != null)
                .Select(m => new ReadReceipt(m.MessageId, m.SenderUserId, m.ReadAt!.Value))
                .FirstOrDefaultAsync(cancellationToken);

            // Cache the result if found
            if (message != null)
            {
                var json = JsonSerializer.Serialize(message);
                await db.StringSetAsync(key, json, ReadReceiptTtl);
            }

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get read receipt for MessageId={MessageId}", messageId);
            return null;
        }
    }

    public async Task<Dictionary<Guid, ReadReceipt>> GetBatchReadReceiptsAsync(
        IEnumerable<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, ReadReceipt>();
        var db = _redis.GetDatabase();

        try
        {
            // Batch get from Redis
            var tasks = messageIds.Select(async id =>
            {
                var key = $"{ReadReceiptKeyPrefix}{id}";
                var value = await db.StringGetAsync(key);
                return (id, value);
            }).ToList();

            var redisResults = await Task.WhenAll(tasks);

            var missingIds = new List<Guid>();

            foreach (var (id, value) in redisResults)
            {
                if (!value.IsNullOrEmpty)
                {
                    var receipt = JsonSerializer.Deserialize<ReadReceipt>(value!);
                    if (receipt != null)
                    {
                        result[id] = receipt;
                    }
                }
                else
                {
                    missingIds.Add(id);
                }
            }

            // Fetch missing from database
            if (missingIds.Any())
            {
                var dbReceipts = await _dbContext.Messages
                    .Where(m => missingIds.Contains(m.MessageId) && m.ReadAt != null)
                    .Select(m => new ReadReceipt(m.MessageId, m.SenderUserId, m.ReadAt!.Value))
                    .ToListAsync(cancellationToken);

                // Cache and add to result
                foreach (var receipt in dbReceipts)
                {
                    result[receipt.MessageId] = receipt;
                    var key = $"{ReadReceiptKeyPrefix}{receipt.MessageId}";
                    var json = JsonSerializer.Serialize(receipt);
                    await db.StringSetAsync(key, json, ReadReceiptTtl);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get batch read receipts for {Count} messages", messageIds.Count());
            throw;
        }
    }

    public async Task<IEnumerable<Guid>> GetReadMessageIdsAsync(
        Guid conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check Redis first
            var db = _redis.GetDatabase();
            var key = $"{ConversationReadKeyPrefix}{conversationId}:{userId}";
            var members = await db.SetMembersAsync(key);

            if (members.Length > 0)
            {
                return members.Select(m => Guid.Parse(m.ToString())).ToList();
            }

            // Fallback to database
            var messageIds = await _dbContext.Messages
                .Where(m => m.ConversationId == conversationId && m.ReadAt != null)
                .Select(m => m.MessageId)
                .ToListAsync(cancellationToken);

            // Warm up cache
            if (messageIds.Any())
            {
                foreach (var msgId in messageIds)
                {
                    await db.SetAddAsync(key, msgId.ToString());
                }
                await db.KeyExpireAsync(key, ReadReceiptTtl);
            }

            return messageIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get read message IDs: ConversationId={ConversationId}, UserId={UserId}",
                conversationId, userId);
            return Enumerable.Empty<Guid>();
        }
    }
}

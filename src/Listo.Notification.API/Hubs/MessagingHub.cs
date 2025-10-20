using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Listo.Notification.API.Hubs;

/// <summary>
/// SignalR hub for real-time in-app messaging (customer-support, customer-driver conversations).
/// Hub path: /hubs/messaging
/// Supports typing indicators, read receipts, presence tracking, and message persistence.
/// </summary>
public class MessagingHub : Hub<IMessagingClient>
{
    private readonly ILogger<MessagingHub> _logger;
    private readonly NotificationDbContext _dbContext;
    private readonly IPresenceTrackingService _presenceService;
    private readonly IReadReceiptService _readReceiptService;
    private readonly ITypingIndicatorService _typingIndicatorService;

    public MessagingHub(
        ILogger<MessagingHub> logger,
        NotificationDbContext dbContext,
        IPresenceTrackingService presenceService,
        IReadReceiptService readReceiptService,
        ITypingIndicatorService typingIndicatorService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _presenceService = presenceService ?? throw new ArgumentNullException(nameof(presenceService));
        _readReceiptService = readReceiptService ?? throw new ArgumentNullException(nameof(readReceiptService));
        _typingIndicatorService = typingIndicatorService ?? throw new ArgumentNullException(nameof(typingIndicatorService));
    }

    /// <summary>
    /// Called when a client connects to the messaging hub.
    /// Automatically adds the user to their conversation groups and tracks presence.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning(
                "Connection rejected: Missing userId or tenantId. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            Context.Abort();
            return;
        }

        // Add to user-specific group for presence tracking
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        // Track presence in Redis
        await _presenceService.SetUserOnlineAsync(userId, Context.ConnectionId);

        _logger.LogInformation(
            "User connected to messaging hub: UserId={UserId}, TenantId={TenantId}, ConnectionId={ConnectionId}",
            userId, tenantId, Context.ConnectionId);

        // Notify other users that this user is now online
        await Clients.Others.UserOnline(userId);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Updates presence tracking in Redis.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();

        if (!string.IsNullOrEmpty(userId))
        {
            // Update presence in Redis
            await _presenceService.SetUserOfflineAsync(userId, Context.ConnectionId);

            // Check if user still has other connections
            var presence = await _presenceService.GetUserPresenceAsync(userId);
            if (!presence.IsOnline)
            {
                // Notify other users that this user is now offline
                await Clients.Others.UserOffline(userId);
            }
        }

        if (exception != null)
        {
            _logger.LogError(exception,
                "User disconnected from messaging hub with error: UserId={UserId}, ConnectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "User disconnected from messaging hub: UserId={UserId}, ConnectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client sends a message in a conversation.
    /// Persists to database first, then broadcasts to all participants.
    /// </summary>
    public async Task SendMessage(string conversationId, string body, string[]? attachments = null)
    {
        var userId = GetUserId();
        var conversationGuid = Guid.Parse(conversationId);
        var sentAt = DateTime.UtcNow;

        // 1. Persist message to database
        var message = new MessageEntity
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversationGuid,
            SenderUserId = userId,
            Body = body,
            AttachmentsJson = attachments != null && attachments.Length > 0 
                ? JsonSerializer.Serialize(attachments)
                : null,
            Status = Listo.Notification.Domain.Enums.MessageStatus.Sent,
            SentAt = sentAt
        };

        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Message saved: ConversationId={ConversationId}, UserId={UserId}, MessageId={MessageId}",
            conversationId, userId, message.MessageId);

        // 2. Clear typing indicator
        await _typingIndicatorService.ClearTypingAsync(conversationId, userId);

        // 3. Refresh user presence
        await _presenceService.RefreshPresenceAsync(userId);

        // 4. Broadcast to all participants in the conversation
        await Clients.Group($"conversation:{conversationId}")
            .OnMessageReceived(new MessageDto(
                message.MessageId.ToString(),
                conversationId,
                userId,
                body,
                attachments ?? Array.Empty<string>(),
                "sent",
                sentAt,
                null));
    }

    /// <summary>
    /// Client indicates they are typing in a conversation.
    /// Stores state in Redis with 10-second TTL.
    /// </summary>
    public async Task StartTyping(string conversationId)
    {
        var userId = GetUserId();

        // Store typing state in Redis
        await _typingIndicatorService.SetTypingAsync(conversationId, userId);

        _logger.LogDebug(
            "User started typing: ConversationId={ConversationId}, UserId={UserId}",
            conversationId, userId);

        // Broadcast to other participants (not the sender)
        await Clients.OthersInGroup($"conversation:{conversationId}")
            .OnTypingIndicator(conversationId, userId, true);
    }

    /// <summary>
    /// Client stops typing in a conversation.
    /// Clears state from Redis.
    /// </summary>
    public async Task StopTyping(string conversationId)
    {
        var userId = GetUserId();

        // Clear typing state from Redis
        await _typingIndicatorService.ClearTypingAsync(conversationId, userId);

        _logger.LogDebug(
            "User stopped typing: ConversationId={ConversationId}, UserId={UserId}",
            conversationId, userId);

        // Broadcast to other participants
        await Clients.OthersInGroup($"conversation:{conversationId}")
            .OnTypingIndicator(conversationId, userId, false);
    }

    /// <summary>
    /// Client marks a message as read.
    /// Persists to database and Redis, then broadcasts.
    /// </summary>
    public async Task MarkAsRead(string conversationId, string messageId)
    {
        var userId = GetUserId();
        var readAt = DateTime.UtcNow;
        var messageGuid = Guid.Parse(messageId);

        // Record read receipt (database + Redis)
        await _readReceiptService.RecordReadReceiptAsync(messageGuid, userId, readAt);

        _logger.LogInformation(
            "Message marked as read: MessageId={MessageId}, ConversationId={ConversationId}, UserId={UserId}",
            messageId, conversationId, userId);

        // Broadcast read receipt to other participants
        await Clients.OthersInGroup($"conversation:{conversationId}")
            .OnMessageRead(conversationId, messageId, userId, readAt);
    }

    /// <summary>
    /// Client joins a conversation room for real-time updates.
    /// Validates that user is a participant in the conversation.
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        var userId = GetUserId();
        var conversationGuid = Guid.Parse(conversationId);

        // Validate user is a participant
        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(c => c.ConversationId == conversationGuid);

        if (conversation == null)
        {
            _logger.LogWarning(
                "Conversation not found: ConversationId={ConversationId}, UserId={UserId}",
                conversationId, userId);
            throw new HubException("Conversation not found");
        }

        // Check if user is a participant
        var participants = JsonSerializer.Deserialize<List<string>>(conversation.ParticipantsJson);
        if (participants == null || !participants.Contains(userId))
        {
            _logger.LogWarning(
                "Unauthorized conversation access: ConversationId={ConversationId}, UserId={UserId}",
                conversationId, userId);
            throw new HubException("You are not a participant in this conversation");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");

        _logger.LogInformation(
            "User joined conversation: ConversationId={ConversationId}, UserId={UserId}",
            conversationId, userId);
    }

    /// <summary>
    /// Client leaves a conversation room.
    /// </summary>
    public async Task LeaveConversation(string conversationId)
    {
        var userId = GetUserId();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");

        _logger.LogInformation(
            "User left conversation: ConversationId={ConversationId}, UserId={UserId}",
            conversationId, userId);
    }

    /// <summary>
    /// Ping method for health checks and connection testing.
    /// </summary>
    public Task<string> Ping()
    {
        return Task.FromResult("pong");
    }

    private string GetUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }

    private string GetTenantId()
    {
        return Context.User?.FindFirst("tenant_id")?.Value ?? string.Empty;
    }
}

/// <summary>
/// Strongly-typed interface for messaging client methods.
/// </summary>
public interface IMessagingClient
{
    /// <summary>
    /// Receives a new message in a conversation.
    /// </summary>
    Task OnMessageReceived(MessageDto message);

    /// <summary>
    /// Receives typing indicator from another user.
    /// </summary>
    Task OnTypingIndicator(string conversationId, string userId, bool isTyping);

    /// <summary>
    /// Receives read receipt for a message.
    /// </summary>
    Task OnMessageRead(string conversationId, string messageId, string userId, DateTime readAt);

    /// <summary>
    /// User comes online.
    /// </summary>
    Task UserOnline(string userId);

    /// <summary>
    /// User goes offline.
    /// </summary>
    Task UserOffline(string userId);
}

/// <summary>
/// Message data transfer object sent via SignalR.
/// </summary>
public record MessageDto(
    string MessageId,
    string ConversationId,
    string SenderUserId,
    string Body,
    string[] Attachments,
    string Status,
    DateTime SentAt,
    DateTime? ReadAt);

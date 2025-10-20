using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Listo.Notification.API.Hubs;

/// <summary>
/// SignalR hub for real-time in-app messaging (customer-support, customer-driver conversations).
/// Hub path: /hubs/messaging
/// Supports typing indicators, read receipts, and presence.
/// </summary>
public class MessagingHub : Hub<IMessagingClient>
{
    private readonly ILogger<MessagingHub> _logger;

    public MessagingHub(ILogger<MessagingHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Called when a client connects to the messaging hub.
    /// Automatically adds the user to their conversation groups.
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

        _logger.LogInformation(
            "User connected to messaging hub: UserId={UserId}, TenantId={TenantId}, ConnectionId={ConnectionId}",
            userId, tenantId, Context.ConnectionId);

        // Notify other users that this user is now online
        await Clients.Others.UserOnline(userId);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();

        if (!string.IsNullOrEmpty(userId))
        {
            // Notify other users that this user is now offline
            await Clients.Others.UserOffline(userId);
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
    /// Server broadcasts to all participants in the conversation.
    /// </summary>
    public async Task SendMessage(string conversationId, string body, string[]? attachments = null)
    {
        var userId = GetUserId();
        var messageId = Guid.NewGuid().ToString();
        var sentAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Message sent: ConversationId={ConversationId}, UserId={UserId}, MessageId={MessageId}",
            conversationId, userId, messageId);

        // Broadcast to all participants in the conversation
        await Clients.Group($"conversation:{conversationId}")
            .OnMessageReceived(new MessageDto(
                messageId,
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
    /// </summary>
    public async Task StartTyping(string conversationId)
    {
        var userId = GetUserId();

        _logger.LogDebug(
            "User started typing: ConversationId={ConversationId}, UserId={UserId}",
            conversationId, userId);

        // Broadcast to other participants (not the sender)
        await Clients.OthersInGroup($"conversation:{conversationId}")
            .OnTypingIndicator(conversationId, userId, true);
    }

    /// <summary>
    /// Client stops typing in a conversation.
    /// </summary>
    public async Task StopTyping(string conversationId)
    {
        var userId = GetUserId();

        _logger.LogDebug(
            "User stopped typing: ConversationId={ConversationId}, UserId={UserId}",
            conversationId, userId);

        // Broadcast to other participants
        await Clients.OthersInGroup($"conversation:{conversationId}")
            .OnTypingIndicator(conversationId, userId, false);
    }

    /// <summary>
    /// Client marks a message as read.
    /// </summary>
    public async Task MarkAsRead(string conversationId, string messageId)
    {
        var userId = GetUserId();
        var readAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Message marked as read: MessageId={MessageId}, ConversationId={ConversationId}, UserId={UserId}",
            messageId, conversationId, userId);

        // Broadcast read receipt to other participants
        await Clients.OthersInGroup($"conversation:{conversationId}")
            .OnMessageRead(conversationId, messageId, userId, readAt);
    }

    /// <summary>
    /// Client joins a conversation room for real-time updates.
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        var userId = GetUserId();

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

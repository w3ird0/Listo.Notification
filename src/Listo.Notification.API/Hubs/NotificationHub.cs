using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Listo.Notification.API.Hubs;

/// <summary>
/// SignalR hub for real-time notification delivery.
/// Supports tenant-scoped groups and user-specific connections.
/// </summary>
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// Automatically adds the user to their tenant group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning(
                "Connection rejected: Missing userId or tenantId. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            Context.Abort();
            return;
        }

        // Add to user-specific group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        
        // Add to tenant-wide group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");

        _logger.LogInformation(
            "User connected: UserId={UserId}, TenantId={TenantId}, ConnectionId={ConnectionId}",
            userId, tenantId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;

        if (exception != null)
        {
            _logger.LogError(exception,
                "User disconnected with error: UserId={UserId}, TenantId={TenantId}, ConnectionId={ConnectionId}",
                userId, tenantId, Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "User disconnected: UserId={UserId}, TenantId={TenantId}, ConnectionId={ConnectionId}",
                userId, tenantId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client confirms receipt of a notification.
    /// </summary>
    /// <param name="notificationId">ID of the notification received.</param>
    public async Task AcknowledgeNotification(string notificationId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation(
            "Notification acknowledged: NotificationId={NotificationId}, UserId={UserId}",
            notificationId, userId);

        // Broadcast acknowledgment to other user sessions
        await Clients.OthersInGroup($"user:{userId}")
            .SendAsync("NotificationAcknowledged", notificationId);
    }

    /// <summary>
    /// Client marks a notification as read.
    /// </summary>
    /// <param name="notificationId">ID of the notification.</param>
    public async Task MarkAsRead(string notificationId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation(
            "Notification marked as read: NotificationId={NotificationId}, UserId={UserId}",
            notificationId, userId);

        // Broadcast read status to other user sessions
        await Clients.OthersInGroup($"user:{userId}")
            .SendAsync("NotificationRead", notificationId, DateTime.UtcNow);
    }

    /// <summary>
    /// Client subscribes to a specific notification channel.
    /// </summary>
    /// <param name="channel">Channel name (e.g., "orders", "rides", "auth").</param>
    public async Task SubscribeToChannel(string channel)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"channel:{tenantId}:{channel}");

        _logger.LogInformation(
            "User subscribed to channel: UserId={UserId}, TenantId={TenantId}, Channel={Channel}",
            userId, tenantId, channel);
    }

    /// <summary>
    /// Client unsubscribes from a specific notification channel.
    /// </summary>
    /// <param name="channel">Channel name to unsubscribe from.</param>
    public async Task UnsubscribeFromChannel(string channel)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"channel:{tenantId}:{channel}");

        _logger.LogInformation(
            "User unsubscribed from channel: UserId={UserId}, TenantId={TenantId}, Channel={Channel}",
            userId, tenantId, channel);
    }

    /// <summary>
    /// Ping method for health checks and connection testing.
    /// </summary>
    public Task<string> Ping()
    {
        return Task.FromResult("pong");
    }
}

/// <summary>
/// Strongly-typed interface for SignalR client methods.
/// </summary>
public interface INotificationClient
{
    /// <summary>
    /// Receives a new notification from the server.
    /// </summary>
    Task ReceiveNotification(NotificationMessage notification);

    /// <summary>
    /// Notification has been acknowledged by another session.
    /// </summary>
    Task NotificationAcknowledged(string notificationId);

    /// <summary>
    /// Notification has been read by another session.
    /// </summary>
    Task NotificationRead(string notificationId, DateTime readAt);

    /// <summary>
    /// Batch of notifications received.
    /// </summary>
    Task ReceiveNotificationBatch(IEnumerable<NotificationMessage> notifications);
}

/// <summary>
/// Notification message sent to clients via SignalR.
/// </summary>
public record NotificationMessage(
    string Id,
    string Channel,
    string Title,
    string Body,
    string Priority,
    DateTime CreatedAt,
    Dictionary<string, string>? Metadata = null);

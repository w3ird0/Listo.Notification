using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Service for orchestrating notification delivery across multiple channels.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification via the specified channel.
    /// </summary>
    Task<SendNotificationResponse> SendNotificationAsync(
        Guid tenantId,
        Guid userId,
        SendNotificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user notifications with pagination.
    /// </summary>
    Task<PagedNotificationsResponse> GetUserNotificationsAsync(
        Guid tenantId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific notification.
    /// </summary>
    Task<NotificationResponse?> GetNotificationByIdAsync(
        Guid tenantId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    Task<bool> MarkAsReadAsync(
        Guid tenantId,
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets notification statistics for a user.
    /// </summary>
    Task<Dictionary<string, int>> GetUserStatisticsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

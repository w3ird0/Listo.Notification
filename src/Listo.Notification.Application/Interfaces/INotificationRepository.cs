using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Repository for notification entity operations with tenant scoping.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Gets a notification by ID within a tenant scope.
    /// </summary>
    Task<NotificationEntity?> GetByIdAsync(Guid tenantId, Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated notifications for a user within a tenant.
    /// </summary>
    Task<(IEnumerable<NotificationEntity> Items, int TotalCount)> GetUserNotificationsAsync(
        Guid tenantId,
        Guid userId,
        int page,
        int pageSize,
        NotificationChannel? channel = null,
        NotificationStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new notification.
    /// </summary>
    Task<NotificationEntity> CreateAsync(NotificationEntity notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing notification.
    /// </summary>
    Task UpdateAsync(NotificationEntity notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    Task MarkAsReadAsync(Guid tenantId, Guid notificationId, DateTime readAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread notification count for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets notification statistics for a user.
    /// </summary>
    Task<Dictionary<string, int>> GetUserStatisticsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

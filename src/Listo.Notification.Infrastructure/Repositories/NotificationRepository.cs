using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;
using Listo.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of notification repository with automatic tenant scoping.
/// </summary>
public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(
        NotificationDbContext context,
        ILogger<NotificationRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NotificationEntity?> GetByIdAsync(
        Guid tenantId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .FirstOrDefaultAsync(
                n => n.TenantId == tenantId && n.Id == notificationId,
                cancellationToken);
    }

    public async Task<(IEnumerable<NotificationEntity> Items, int TotalCount)> GetUserNotificationsAsync(
        Guid tenantId,
        Guid userId,
        int page,
        int pageSize,
        NotificationChannel? channel = null,
        NotificationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications
            .Where(n => n.TenantId == tenantId && n.UserId == userId);

        // Apply filters
        if (channel.HasValue)
        {
            query = query.Where(n => n.Channel == channel.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(n => n.Status == status.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination and ordering
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<NotificationEntity> CreateAsync(
        NotificationEntity notification,
        CancellationToken cancellationToken = default)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notification created: Id={NotificationId}, TenantId={TenantId}, UserId={UserId}, Channel={Channel}",
            notification.Id, notification.TenantId, notification.UserId, notification.Channel);

        return notification;
    }

    public async Task UpdateAsync(
        NotificationEntity notification,
        CancellationToken cancellationToken = default)
    {
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notification updated: Id={NotificationId}, Status={Status}",
            notification.Id, notification.Status);
    }

    public async Task MarkAsReadAsync(
        Guid tenantId,
        Guid notificationId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        var notification = await GetByIdAsync(tenantId, notificationId, cancellationToken);
        
        if (notification != null)
        {
            notification.ReadAt = readAt;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Notification marked as read: Id={NotificationId}, ReadAt={ReadAt}",
                notificationId, readAt);
        }
    }

    public async Task<int> GetUnreadCountAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(
                n => n.TenantId == tenantId 
                    && n.UserId == userId 
                    && n.ReadAt == null,
                cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetUserStatisticsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _context.Notifications
            .Where(n => n.TenantId == tenantId && n.UserId == userId)
            .ToListAsync(cancellationToken);

        return new Dictionary<string, int>
        {
            ["total"] = notifications.Count,
            ["unread"] = notifications.Count(n => n.ReadAt == null),
            ["sms"] = notifications.Count(n => n.Channel == NotificationChannel.Sms),
            ["email"] = notifications.Count(n => n.Channel == NotificationChannel.Email),
            ["push"] = notifications.Count(n => n.Channel == NotificationChannel.Push),
            ["inapp"] = notifications.Count(n => n.Channel == NotificationChannel.InApp),
            ["sent"] = notifications.Count(n => n.Status == NotificationStatus.Sent),
            ["delivered"] = notifications.Count(n => n.Status == NotificationStatus.Delivered),
            ["failed"] = notifications.Count(n => n.Status == NotificationStatus.Failed)
        };
    }

    public async Task<IEnumerable<NotificationEntity>> GetScheduledNotificationsAsync(
        DateTime scheduledBefore,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _context.Notifications
            .Where(n => 
                n.Status == NotificationStatus.Queued &&
                n.ScheduledFor != null &&
                n.ScheduledFor <= scheduledBefore)
            .OrderBy(n => n.ScheduledFor)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} scheduled notifications ready to send (scheduled before {ScheduledBefore})",
            notifications.Count, scheduledBefore);

        return notifications;
    }
}

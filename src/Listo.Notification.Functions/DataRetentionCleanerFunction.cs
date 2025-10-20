using Listo.Notification.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Functions;

/// <summary>
/// Soft-deletes old notifications, messages, and audit logs based on retention policies.
/// Timer interval configurable (default: weekly).
/// Implements safety limits to prevent accidental mass deletions.
/// Singleton to prevent concurrent execution.
/// </summary>
public class DataRetentionCleanerFunction
{
    private readonly ILogger<DataRetentionCleanerFunction> _logger;
    private readonly NotificationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public DataRetentionCleanerFunction(
        ILogger<DataRetentionCleanerFunction> logger,
        NotificationDbContext dbContext,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Timer-triggered function for data retention cleanup.
    /// Timer schedule: Configurable via DataRetentionCleaner:Schedule (default: "0 0 3 * * 0" = 3 AM UTC every Sunday).
    /// Singleton mode to prevent concurrent execution.
    /// </summary>
    [Function("DataRetentionCleaner")]
    [Singleton]
    public async Task RunAsync(
        [TimerTrigger("%DataRetentionCleaner:Schedule%", RunOnStartup = false)] TimerInfo timer,
        FunctionContext context)
    {
        _logger.LogInformation("DataRetentionCleaner started at {Time}", DateTime.UtcNow);

        try
        {
            // Configurable retention periods (in days)
            var notificationRetentionDays = _configuration.GetValue<int>("DataRetention:NotificationDays", 90);
            var messageRetentionDays = _configuration.GetValue<int>("DataRetention:MessageDays", 90);
            var auditLogRetentionDays = _configuration.GetValue<int>("DataRetention:AuditLogDays", 365);
            var queueRetentionDays = _configuration.GetValue<int>("DataRetention:QueueDays", 30);

            // Safety limit: max records to soft-delete per execution
            var safetyLimit = _configuration.GetValue<int>("DataRetention:SafetyLimit", 10000);

            _logger.LogInformation(
                "Retention policies: Notifications={NotifDays}d, Messages={MsgDays}d, " +
                "AuditLogs={AuditDays}d, Queue={QueueDays}d, SafetyLimit={Limit}",
                notificationRetentionDays, messageRetentionDays, auditLogRetentionDays, 
                queueRetentionDays, safetyLimit);

            var now = DateTime.UtcNow;
            var cleanupStats = new CleanupStats();

            // 1. Soft-delete old notifications
            var notificationCutoff = now.AddDays(-notificationRetentionDays);
            cleanupStats.NotificationsDeleted = await SoftDeleteNotificationsAsync(notificationCutoff, safetyLimit);

            // 2. Soft-delete old messages
            var messageCutoff = now.AddDays(-messageRetentionDays);
            cleanupStats.MessagesDeleted = await SoftDeleteMessagesAsync(messageCutoff, safetyLimit);

            // 3. Soft-delete old audit logs
            var auditLogCutoff = now.AddDays(-auditLogRetentionDays);
            cleanupStats.AuditLogsDeleted = await SoftDeleteAuditLogsAsync(auditLogCutoff, safetyLimit);

            // 4. Soft-delete old queue entries
            var queueCutoff = now.AddDays(-queueRetentionDays);
            cleanupStats.QueueEntriesDeleted = await SoftDeleteQueueEntriesAsync(queueCutoff, safetyLimit);

            _logger.LogInformation(
                "DataRetentionCleaner completed. Stats: Notifications={NotifCount}, Messages={MsgCount}, " +
                "AuditLogs={AuditCount}, QueueEntries={QueueCount}",
                cleanupStats.NotificationsDeleted, cleanupStats.MessagesDeleted,
                cleanupStats.AuditLogsDeleted, cleanupStats.QueueEntriesDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DataRetentionCleaner");
            throw;
        }

        if (timer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next data retention cleanup at: {NextRun}", timer.ScheduleStatus.Next);
        }
    }

    /// <summary>
    /// Soft-deletes notifications older than cutoff date.
    /// </summary>
    private async Task<int> SoftDeleteNotificationsAsync(DateTime cutoff, int safetyLimit)
    {
        var notifications = await _dbContext.Notifications
            .Where(n => !n.IsDeleted && n.CreatedAt < cutoff)
            .Take(safetyLimit)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsDeleted = true;
            notification.DeletedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Soft-deleted {Count} notifications older than {Cutoff}",
            notifications.Count, cutoff);

        return notifications.Count;
    }

    /// <summary>
    /// Soft-deletes messages older than cutoff date.
    /// </summary>
    private async Task<int> SoftDeleteMessagesAsync(DateTime cutoff, int safetyLimit)
    {
        var messages = await _dbContext.Messages
            .Where(m => !m.IsDeleted && m.SentAt < cutoff)
            .Take(safetyLimit)
            .ToListAsync();

        foreach (var message in messages)
        {
            message.IsDeleted = true;
            message.DeletedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Soft-deleted {Count} messages older than {Cutoff}",
            messages.Count, cutoff);

        return messages.Count;
    }

    /// <summary>
    /// Soft-deletes audit logs older than cutoff date.
    /// </summary>
    private async Task<int> SoftDeleteAuditLogsAsync(DateTime cutoff, int safetyLimit)
    {
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => !a.IsDeleted && a.OccurredAt < cutoff)
            .Take(safetyLimit)
            .ToListAsync();

        foreach (var log in auditLogs)
        {
            log.IsDeleted = true;
            log.DeletedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Soft-deleted {Count} audit logs older than {Cutoff}",
            auditLogs.Count, cutoff);

        return auditLogs.Count;
    }

    /// <summary>
    /// Soft-deletes notification queue entries older than cutoff date.
    /// </summary>
    private async Task<int> SoftDeleteQueueEntriesAsync(DateTime cutoff, int safetyLimit)
    {
        var queueEntries = await _dbContext.NotificationQueue
            .Where(q => !q.IsDeleted && q.CreatedAt < cutoff)
            .Take(safetyLimit)
            .ToListAsync();

        foreach (var entry in queueEntries)
        {
            entry.IsDeleted = true;
            entry.DeletedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Soft-deleted {Count} queue entries older than {Cutoff}",
            queueEntries.Count, cutoff);

        return queueEntries.Count;
    }
}

/// <summary>
/// Statistics for cleanup operation.
/// </summary>
internal record CleanupStats
{
    public int NotificationsDeleted { get; set; }
    public int MessagesDeleted { get; set; }
    public int AuditLogsDeleted { get; set; }
    public int QueueEntriesDeleted { get; set; }
}

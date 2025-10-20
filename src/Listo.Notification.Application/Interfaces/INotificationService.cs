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

    // Batch Operations
    Task<BatchSendResponse> SendBatchAsync(
        Guid tenantId,
        Guid userId,
        BatchSendRequest request,
        CancellationToken cancellationToken = default);

    Task<BatchScheduleResponse> ScheduleBatchAsync(
        Guid tenantId,
        Guid userId,
        BatchScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<BatchStatusResponse?> GetBatchStatusAsync(
        Guid tenantId,
        string batchId,
        CancellationToken cancellationToken = default);

    // Scheduling
    Task<SendNotificationResponse> ScheduleNotificationAsync(
        Guid tenantId,
        Guid userId,
        ScheduleNotificationRequest request,
        CancellationToken cancellationToken = default);

    // Cancellation
    Task<NotificationResponse?> CancelNotificationAsync(
        Guid tenantId,
        Guid userId,
        Guid notificationId,
        string cancellationReason,
        CancellationToken cancellationToken = default);

    // Internal/Service-to-Service
    Task<QueueNotificationResponse> QueueNotificationAsync(
        InternalNotificationRequest request,
        string serviceName,
        CancellationToken cancellationToken = default);

    Task ProcessCloudEventAsync(
        object cloudEvent,
        string serviceName,
        CancellationToken cancellationToken = default);

    Task<HealthCheckResponse> GetHealthAsync(
        CancellationToken cancellationToken = default);

    // Preferences
    Task<PreferencesResponse> GetPreferencesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PreferencesResponse> UpdatePreferencesAsync(
        Guid tenantId,
        Guid userId,
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default);

    Task<PreferencesResponse> PatchPreferencesAsync(
        Guid tenantId,
        Guid userId,
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default);

    // Admin Operations
    Task<object> GetBudgetsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task UpdateBudgetAsync(
        UpdateBudgetRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedNotificationsResponse> GetFailedNotificationsAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? channel,
        DateTime? failedAfter,
        CancellationToken cancellationToken = default);

    Task<bool> RetryNotificationAsync(
        Guid tenantId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<object> GetStatisticsAsync(
        Guid tenantId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);

    Task<PagedNotificationsResponse> GetNotificationsAsync(
        Guid tenantId,
        Guid userId,
        int pageNumber,
        int pageSize,
        string? channel,
        string? status,
        CancellationToken cancellationToken = default);
}

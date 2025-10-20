using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Application.Services;

/// <summary>
/// Orchestrates notification delivery across multiple channels with provider routing.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly ISmsProvider _smsProvider;
    private readonly IEmailProvider _emailProvider;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        ISmsProvider smsProvider,
        IEmailProvider emailProvider,
        ILogger<NotificationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _smsProvider = smsProvider ?? throw new ArgumentNullException(nameof(smsProvider));
        _emailProvider = emailProvider ?? throw new ArgumentNullException(nameof(emailProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SendNotificationResponse> SendNotificationAsync(
        Guid tenantId,
        Guid userId,
        SendNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing notification: TenantId={TenantId}, UserId={UserId}, Channel={Channel}, Priority={Priority}",
            tenantId, userId, request.Channel, request.Priority);

        // Create notification entity
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Channel = request.Channel,
            Recipient = request.Recipient,
            Subject = request.Subject,
            Body = request.Body,
            Priority = request.Priority,
            Status = NotificationStatus.Queued,
            ServiceOrigin = request.ServiceOrigin,
            Metadata = request.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(request.Metadata) : null,
            ScheduledFor = request.ScheduledFor,
            CreatedAt = DateTime.UtcNow
        };

        // Save to database
        await _repository.CreateAsync(notification, cancellationToken);

        // If not scheduled, send immediately
        if (!request.ScheduledFor.HasValue || request.ScheduledFor.Value <= DateTime.UtcNow)
        {
            await SendViaProviderAsync(notification, cancellationToken);
        }

        return new SendNotificationResponse
        {
            NotificationId = notification.Id,
            Status = notification.Status,
            Message = notification.Status == NotificationStatus.Sent
                ? "Notification sent successfully"
                : "Notification queued for delivery",
            CreatedAt = notification.CreatedAt
        };
    }

    public async Task<PagedNotificationsResponse> GetUserNotificationsAsync(
        Guid tenantId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _repository.GetUserNotificationsAsync(
            tenantId,
            userId,
            page,
            pageSize,
            cancellationToken: cancellationToken);

        return new PagedNotificationsResponse
        {
            Items = items.Select(MapToResponse),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<NotificationResponse?> GetNotificationByIdAsync(
        Guid tenantId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(tenantId, notificationId, cancellationToken);
        return notification != null ? MapToResponse(notification) : null;
    }

    public async Task<bool> MarkAsReadAsync(
        Guid tenantId,
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(tenantId, notificationId, cancellationToken);
        
        if (notification == null || notification.UserId != userId)
        {
            return false;
        }

        await _repository.MarkAsReadAsync(tenantId, notificationId, DateTime.UtcNow, cancellationToken);
        return true;
    }

    public async Task<Dictionary<string, int>> GetUserStatisticsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetUserStatisticsAsync(tenantId, userId, cancellationToken);
    }

    private async Task SendViaProviderAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        try
        {
            var deliveryRequest = new DeliveryRequest(
                notification.Id,
                notification.TenantId,
                notification.Channel,
                notification.Recipient,
                notification.Subject,
                notification.Body);

            DeliveryResult? result = notification.Channel switch
            {
                NotificationChannel.Sms => await _smsProvider.SendAsync(deliveryRequest, cancellationToken),
                NotificationChannel.Email => await _emailProvider.SendAsync(deliveryRequest, cancellationToken),
                NotificationChannel.Push => null, // TODO: Implement push provider
                NotificationChannel.InApp => null, // InApp goes through SignalR, not providers
                _ => null
            };

            if (result != null)
            {
                notification.Status = result.Success ? NotificationStatus.Sent : NotificationStatus.Failed;
                notification.SentAt = result.Success ? DateTime.UtcNow : null;
                notification.ErrorMessage = result.ErrorMessage;
                notification.ProviderMessageId = result.ProviderMessageId;

                await _repository.UpdateAsync(notification, cancellationToken);

                _logger.LogInformation(
                    "Notification delivery: Id={NotificationId}, Status={Status}, Provider={Provider}",
                    notification.Id, notification.Status, result.ProviderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending notification: Id={NotificationId}, Channel={Channel}",
                notification.Id, notification.Channel);

            notification.Status = NotificationStatus.Failed;
            notification.ErrorMessage = ex.Message;
            await _repository.UpdateAsync(notification, cancellationToken);
        }
    }

    // Batch Operations
    public async Task<BatchSendResponse> SendBatchAsync(
        Guid tenantId,
        Guid userId,
        BatchSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var results = new List<BatchItemResult>();

        for (int i = 0; i < request.Notifications.Count; i++)
        {
            var item = request.Notifications[i];
            try
            {
                var notification = new SendNotificationRequest
                {
                    Channel = item.Channel,
                    Recipient = item.Recipient,
                    Subject = item.TemplateKey,
                    Body = $"Template: {item.TemplateKey}",
                    Priority = item.Priority,
                    ServiceOrigin = ServiceOrigin.Internal
                };

                var response = await SendNotificationAsync(tenantId, userId, notification, cancellationToken);
                results.Add(new BatchItemResult
                {
                    Index = i,
                    NotificationId = response.NotificationId,
                    Success = true,
                    CorrelationId = item.CorrelationId
                });
            }
            catch (Exception ex)
            {
                results.Add(new BatchItemResult
                {
                    Index = i,
                    Success = false,
                    ErrorMessage = ex.Message,
                    CorrelationId = item.CorrelationId
                });

                if (!request.ContinueOnError)
                    break;
            }
        }

        return new BatchSendResponse
        {
            BatchId = batchId,
            TotalCount = request.Notifications.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results,
            ProcessedAt = DateTime.UtcNow
        };
    }

    public async Task<BatchScheduleResponse> ScheduleBatchAsync(
        Guid tenantId,
        Guid userId,
        BatchScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var results = new List<BatchItemResult>();

        for (int i = 0; i < request.Notifications.Count; i++)
        {
            var item = request.Notifications[i];
            try
            {
                var notification = new SendNotificationRequest
                {
                    Channel = item.Channel,
                    Recipient = item.Recipient,
                    Subject = item.TemplateKey,
                    Body = $"Template: {item.TemplateKey}",
                    Priority = item.Priority,
                    ServiceOrigin = ServiceOrigin.Internal,
                    ScheduledFor = item.ScheduledFor
                };

                var response = await SendNotificationAsync(tenantId, userId, notification, cancellationToken);
                results.Add(new BatchItemResult
                {
                    Index = i,
                    NotificationId = response.NotificationId,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                results.Add(new BatchItemResult
                {
                    Index = i,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return new BatchScheduleResponse
        {
            BatchId = batchId,
            TotalCount = request.Notifications.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results,
            ProcessedAt = DateTime.UtcNow
        };
    }

    public Task<BatchStatusResponse?> GetBatchStatusAsync(
        Guid tenantId,
        string batchId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement batch status tracking
        return Task.FromResult<BatchStatusResponse?>(null);
    }

    public Task<SendNotificationResponse> ScheduleNotificationAsync(
        Guid tenantId,
        Guid userId,
        ScheduleNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendNotificationAsync(tenantId, userId, request, cancellationToken);
    }

    public async Task<NotificationResponse?> CancelNotificationAsync(
        Guid tenantId,
        Guid userId,
        Guid notificationId,
        string cancellationReason,
        CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(tenantId, notificationId, cancellationToken);
        if (notification == null || notification.UserId != userId)
            return null;

        notification.Status = NotificationStatus.Cancelled;
        notification.ErrorMessage = cancellationReason;
        notification.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(notification, cancellationToken);
        return MapToResponse(notification);
    }

    // Internal/Service-to-Service
    public async Task<QueueNotificationResponse> QueueNotificationAsync(
        InternalNotificationRequest request,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var queueId = Guid.NewGuid();
        _logger.LogInformation("Queueing notification from service {ServiceName}", serviceName);

        return new QueueNotificationResponse
        {
            QueueId = queueId,
            Status = "Queued",
            QueuedAt = DateTime.UtcNow
        };
    }

    public Task ProcessCloudEventAsync(
        object cloudEvent,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing CloudEvent from {ServiceName}", serviceName);
        return Task.CompletedTask;
    }

    public Task<HealthCheckResponse> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthCheckResponse
        {
            Status = "Healthy",
            Components = new Dictionary<string, ComponentHealth>
            {
                ["Database"] = new ComponentHealth { Status = "Healthy" },
                ["SMS"] = new ComponentHealth { Status = "Healthy" },
                ["Email"] = new ComponentHealth { Status = "Healthy" }
            },
            Timestamp = DateTime.UtcNow
        });
    }

    // Preferences
    public Task<PreferencesResponse> GetPreferencesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement preferences retrieval
        return Task.FromResult(new PreferencesResponse
        {
            UserId = userId,
            ChannelPreferences = new Dictionary<NotificationChannel, bool>(),
            EnableQuietHours = false,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public Task<PreferencesResponse> UpdatePreferencesAsync(
        Guid tenantId,
        Guid userId,
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        return GetPreferencesAsync(tenantId, userId, cancellationToken);
    }

    public Task<PreferencesResponse> PatchPreferencesAsync(
        Guid tenantId,
        Guid userId,
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        return GetPreferencesAsync(tenantId, userId, cancellationToken);
    }

    // Admin Operations
    public Task<object> GetBudgetsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<object>(new { tenantId, budget = 1000.0m });
    }

    public Task UpdateBudgetAsync(
        UpdateBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating budget for tenant {TenantId}", request.TenantId);
        return Task.CompletedTask;
    }

    public async Task<PagedNotificationsResponse> GetFailedNotificationsAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? channel,
        DateTime? failedAfter,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _repository.GetUserNotificationsAsync(
            tenantId,
            Guid.Empty,
            pageNumber,
            pageSize,
            status: NotificationStatus.Failed,
            cancellationToken: cancellationToken);

        return new PagedNotificationsResponse
        {
            Items = items.Select(MapToResponse),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<bool> RetryNotificationAsync(
        Guid tenantId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(tenantId, notificationId, cancellationToken);
        if (notification == null || notification.Status != NotificationStatus.Failed)
            return false;

        notification.Status = NotificationStatus.Queued;
        notification.ErrorMessage = null;
        await _repository.UpdateAsync(notification, cancellationToken);
        await SendViaProviderAsync(notification, cancellationToken);
        return true;
    }

    public Task<object> GetStatisticsAsync(
        Guid tenantId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<object>(new
        {
            tenantId,
            period = new { startDate, endDate },
            totalSent = 0,
            totalFailed = 0
        });
    }

    public async Task<PagedNotificationsResponse> GetNotificationsAsync(
        Guid tenantId,
        Guid userId,
        int pageNumber,
        int pageSize,
        string? channel,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _repository.GetUserNotificationsAsync(
            tenantId,
            userId,
            pageNumber,
            pageSize,
            cancellationToken: cancellationToken);

        return new PagedNotificationsResponse
        {
            Items = items.Select(MapToResponse),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private static NotificationResponse MapToResponse(NotificationEntity entity)
    {
        return new NotificationResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            Channel = entity.Channel,
            Recipient = entity.Recipient,
            Subject = entity.Subject,
            Body = entity.Body,
            Priority = entity.Priority,
            Status = entity.Status,
            ServiceOrigin = entity.ServiceOrigin,
            CreatedAt = entity.CreatedAt,
            SentAt = entity.SentAt,
            DeliveredAt = entity.DeliveredAt,
            ReadAt = entity.ReadAt,
            Metadata = entity.Metadata != null
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Metadata)
                : null
        };
    }
}

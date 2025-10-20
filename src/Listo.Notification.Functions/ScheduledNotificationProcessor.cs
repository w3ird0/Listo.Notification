using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Functions;

/// <summary>
/// Azure Function for processing scheduled notifications.
/// Runs every minute to check for notifications that need to be sent.
/// </summary>
public class ScheduledNotificationProcessor
{
    private readonly ILogger<ScheduledNotificationProcessor> _logger;
    private readonly INotificationRepository _notificationRepository;
    private readonly ISmsProvider _smsProvider;
    private readonly IEmailProvider _emailProvider;
    private readonly IPushProvider _pushProvider;

    public ScheduledNotificationProcessor(
        ILogger<ScheduledNotificationProcessor> logger,
        INotificationRepository notificationRepository,
        ISmsProvider smsProvider,
        IEmailProvider emailProvider,
        IPushProvider pushProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _smsProvider = smsProvider ?? throw new ArgumentNullException(nameof(smsProvider));
        _emailProvider = emailProvider ?? throw new ArgumentNullException(nameof(emailProvider));
        _pushProvider = pushProvider ?? throw new ArgumentNullException(nameof(pushProvider));
    }

    /// <summary>
    /// Timer-triggered function that runs every minute.
    /// CRON: "0 */1 * * * *" = every minute
    /// </summary>
    [Function(nameof(ScheduledNotificationProcessor))]
    public async Task Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo,
        FunctionContext context)
    {
        _logger.LogInformation("Scheduled notification processor started at: {Time}", DateTime.UtcNow);
        
        try
        {
            var now = DateTime.UtcNow;
            var scheduledNotifications = await _notificationRepository.GetScheduledNotificationsAsync(now);
            
            var notificationList = scheduledNotifications.ToList();
            _logger.LogInformation("Found {Count} scheduled notifications to process", notificationList.Count);
            
            foreach (var notification in notificationList)
            {
                await ProcessNotificationAsync(notification);
            }
            
            _logger.LogInformation(
                "Scheduled notification processor completed successfully. Processed {Count} notifications",
                notificationList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled notifications");
            throw;
        }

        if (timerInfo.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {NextRun}", timerInfo.ScheduleStatus.Next);
        }
    }

    /// <summary>
    /// Processes an individual notification by routing to the appropriate provider.
    /// </summary>
    private async Task ProcessNotificationAsync(NotificationEntity notification)
    {
        _logger.LogInformation(
            "Processing notification: Id={NotificationId}, Channel={Channel}, TenantId={TenantId}",
            notification.Id, notification.Channel, notification.TenantId);

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
                NotificationChannel.Sms => await _smsProvider.SendAsync(deliveryRequest),
                NotificationChannel.Email => await _emailProvider.SendAsync(deliveryRequest),
                NotificationChannel.Push => await _pushProvider.SendAsync(deliveryRequest),
                NotificationChannel.InApp => null, // InApp goes through SignalR
                _ => null
            };

            if (result != null)
            {
                // Update notification status
                notification.Status = result.Success ? NotificationStatus.Sent : NotificationStatus.Failed;
                notification.SentAt = result.Success ? DateTime.UtcNow : null;
                notification.ErrorMessage = result.ErrorMessage;
                notification.ProviderMessageId = result.ProviderMessageId;

                await _notificationRepository.UpdateAsync(notification);

                _logger.LogInformation(
                    "Notification processed: Id={NotificationId}, Status={Status}, Provider={Provider}",
                    notification.Id, notification.Status, result.ProviderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing notification: Id={NotificationId}, Channel={Channel}",
                notification.Id, notification.Channel);

            notification.Status = NotificationStatus.Failed;
            notification.ErrorMessage = ex.Message;
            await _notificationRepository.UpdateAsync(notification);
        }
    }
}

/// <summary>
/// Timer information for Azure Functions
/// </summary>
public class TimerInfo
{
    public TimerScheduleStatus? ScheduleStatus { get; set; }
    public bool IsPastDue { get; set; }
}

public class TimerScheduleStatus
{
    public DateTime Last { get; set; }
    public DateTime Next { get; set; }
    public DateTime LastUpdated { get; set; }
}

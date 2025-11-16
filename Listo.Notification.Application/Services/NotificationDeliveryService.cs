using Listo.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Application.Services;

/// <summary>
/// Determines delivery mode and routes notifications appropriately.
/// Sync delivery: driver assignment, OTP/2FA with 2s timeout and immediate failure.
/// Async delivery: queued to Service Bus for background processing.
/// </summary>
public class NotificationDeliveryService
{
    private readonly ILogger<NotificationDeliveryService> _logger;

    public NotificationDeliveryService(ILogger<NotificationDeliveryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines the appropriate delivery mode based on priority, template key, and channel.
    /// </summary>
    public DeliveryMode DetermineDeliveryMode(NotificationRequest request)
    {
        // Synchronous criteria: driver assignment, OTP, 2FA, or critical priority
        if (IsSynchronousDelivery(request))
        {
            _logger.LogInformation(
                "Routing notification {NotificationId} for SYNCHRONOUS delivery (template: {TemplateKey}, priority: {Priority})",
                request.NotificationId, request.TemplateKey, request.Priority);

            return DeliveryMode.Synchronous;
        }

        // Priority queue for high-priority notifications
        if (request.Priority == Priority.High)
        {
            _logger.LogInformation(
                "Routing notification {NotificationId} to PRIORITY queue",
                request.NotificationId);

            return DeliveryMode.PriorityQueue;
        }

        // Bulk queue for low-priority or bulk notifications
        if (request.IsBulk || request.Priority == Priority.Low)
        {
            _logger.LogInformation(
                "Routing notification {NotificationId} to BULK queue",
                request.NotificationId);

            return DeliveryMode.BulkQueue;
        }

        // Default: standard async queue
        _logger.LogInformation(
            "Routing notification {NotificationId} to STANDARD queue",
            request.NotificationId);

        return DeliveryMode.StandardQueue;
    }

    /// <summary>
    /// Checks if a notification requires synchronous delivery.
    /// Sync delivery: driver_assign, otp, 2fa templates, or explicitly marked synchronous.
    /// </summary>
    private bool IsSynchronousDelivery(NotificationRequest request)
    {
        // Explicitly marked as synchronous
        if (request.Synchronous)
            return true;

        // Template-based detection
        var templateKeyLower = request.TemplateKey.ToLowerInvariant();

        return templateKeyLower.Contains("driver_assign") ||
               templateKeyLower.Contains("driver_assigned") ||
               templateKeyLower.Contains("otp") ||
               templateKeyLower.Contains("2fa") ||
               templateKeyLower.Contains("two_factor");
    }

    /// <summary>
    /// Gets the Service Bus queue name for the delivery mode.
    /// </summary>
    public string GetQueueName(DeliveryMode mode) => mode switch
    {
        DeliveryMode.PriorityQueue => "listo-notifications-priority",
        DeliveryMode.StandardQueue => "listo-notifications-queue",
        DeliveryMode.BulkQueue => "listo-notifications-bulk",
        DeliveryMode.Synchronous => throw new InvalidOperationException(
            "Synchronous delivery does not use queues"),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    /// <summary>
    /// Gets the maximum latency expected for each delivery mode.
    /// </summary>
    public TimeSpan GetExpectedLatency(DeliveryMode mode) => mode switch
    {
        DeliveryMode.Synchronous => TimeSpan.FromSeconds(2),
        DeliveryMode.PriorityQueue => TimeSpan.FromSeconds(5),
        DeliveryMode.StandardQueue => TimeSpan.FromSeconds(30),
        DeliveryMode.BulkQueue => TimeSpan.FromMinutes(5),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };
}

/// <summary>
/// Delivery mode for notification routing.
/// </summary>
public enum DeliveryMode
{
    /// <summary>
    /// Synchronous delivery with 2s timeout (driver assignment, OTP, 2FA).
    /// Immediate failure on timeout - NO automatic queueing.
    /// </summary>
    Synchronous,

    /// <summary>
    /// High-priority queue with max 5s latency.
    /// Used for high-priority but non-critical notifications.
    /// </summary>
    PriorityQueue,

    /// <summary>
    /// Standard async queue with max 30s latency.
    /// Default for most notifications.
    /// </summary>
    StandardQueue,

    /// <summary>
    /// Bulk/low-priority queue with up to 5min latency.
    /// Used for marketing, newsletters, batch notifications.
    /// </summary>
    BulkQueue
}

/// <summary>
/// Notification request for delivery routing.
/// </summary>
public record NotificationRequest
{
    public required Guid NotificationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string UserId { get; init; }
    public required string ServiceOrigin { get; init; }
    public required string Channel { get; init; }
    public required string TemplateKey { get; init; }
    public required Priority Priority { get; init; }
    public bool Synchronous { get; init; }
    public bool IsBulk { get; init; }
    public Dictionary<string, object>? Data { get; init; }
    public string? RenderedContent { get; init; }
    public string? Locale { get; init; }
}

/// <summary>
/// Result of a delivery operation with routing information.
/// </summary>
public record NotificationDeliveryResult
{
    public required bool Success { get; init; }
    public string? ProviderMessageId { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ProviderName { get; init; }

    public static NotificationDeliveryResult Succeeded(string providerMessageId, string providerName) =>
        new()
        {
            Success = true,
            ProviderMessageId = providerMessageId,
            DeliveredAt = DateTime.UtcNow,
            ProviderName = providerName
        };

    public static NotificationDeliveryResult Failed(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

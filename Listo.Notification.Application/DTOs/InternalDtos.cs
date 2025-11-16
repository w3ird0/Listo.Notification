using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Internal service-to-service notification request (requires X-Service-Secret header).
/// Supports both pre-rendered content and template-based rendering.
/// </summary>
public record InternalNotificationRequest : SendNotificationRequest
{
    public required string ServiceName { get; init; }
    public string? EventType { get; init; }

    // Template-based flow (preferred)
    /// <summary>
    /// Template key for rendering (e.g., "email_verification", "driver_new_order_available").
    /// If provided, template will be rendered with Variables and Locale.
    /// </summary>
    public string? TemplateKey { get; init; }

    /// <summary>
    /// Variables for template rendering.
    /// </summary>
    public Dictionary<string, object>? Variables { get; init; }

    /// <summary>
    /// Locale for template rendering (default: "en").
    /// </summary>
    public string? Locale { get; init; } = "en";

    // Synchronous delivery support
    /// <summary>
    /// If true, notification is sent immediately and delivery result is returned.
    /// If false (default), notification is queued for async processing via Service Bus.
    /// Synchronous delivery only supports SMS, Email, and Push channels (In-App rejected).
    /// </summary>
    public bool Synchronous { get; init; } = false;
}

/// <summary>
/// Internal request to queue a notification for async processing.
/// </summary>
public record QueueNotificationRequest
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required ServiceOrigin ServiceOrigin { get; init; }
    public required NotificationChannel Channel { get; init; }
    public required string TemplateKey { get; init; }
    public required Dictionary<string, object> Variables { get; init; }
    public Priority Priority { get; init; } = Priority.Normal;
    public bool Synchronous { get; init; } = false;
    public DateTime? ScheduledFor { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Internal response after queueing.
/// </summary>
public record QueueNotificationResponse
{
    public required Guid QueueId { get; init; }
    public required string Status { get; init; }
    public required DateTime QueuedAt { get; init; }

    // Synchronous delivery fields
    /// <summary>
    /// Timestamp when notification was sent (only for synchronous delivery).
    /// </summary>
    public DateTime? SentAt { get; init; }

    /// <summary>
    /// Delivery status: "Queued", "Sent", "Failed", "Timeout" (for synchronous delivery).
    /// </summary>
    public string? DeliveryStatus { get; init; }

    /// <summary>
    /// Delivery details or error message (for synchronous delivery).
    /// </summary>
    public string? DeliveryDetails { get; init; }
}

/// <summary>
/// Health check response.
/// </summary>
public record HealthCheckResponse
{
    public required string Status { get; init; } // "Healthy", "Degraded", "Unhealthy"
    public required Dictionary<string, ComponentHealth> Components { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Health status for individual component.
/// </summary>
public record ComponentHealth
{
    public required string Status { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, object>? Data { get; init; }
}

/// <summary>
/// Request to queue multiple notifications in a batch.
/// </summary>
public record BatchInternalNotificationRequest
{
    public required string ServiceName { get; init; }
    public string? EventType { get; init; }
    public required IEnumerable<InternalNotificationRequest> Notifications { get; init; }
}

/// <summary>
/// Individual result in batch response.
/// </summary>
public record QueueNotificationResult
{
    public required int Index { get; init; }
    public required bool Success { get; init; }
    public Guid? QueueId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Response after queueing batch notifications.
/// </summary>
public record BatchQueueNotificationResponse
{
    public required int TotalRequested { get; init; }
    public required int QueuedCount { get; init; }
    public required int FailedCount { get; init; }
    public required IEnumerable<QueueNotificationResult> Results { get; init; }
    public required DateTime ProcessedAt { get; init; }
}

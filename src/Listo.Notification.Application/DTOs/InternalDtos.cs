using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Internal service-to-service notification request (requires X-Service-Secret header).
/// </summary>
public record InternalNotificationRequest : SendNotificationRequest
{
    public required string ServiceName { get; init; }
    public string? EventType { get; init; }
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

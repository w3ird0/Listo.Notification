using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Request to send notifications in batch.
/// </summary>
public record BatchSendRequest
{
    public required List<BatchNotificationItem> Notifications { get; init; }
    public bool ContinueOnError { get; init; } = true;
}

/// <summary>
/// Individual notification item in a batch.
/// </summary>
public record BatchNotificationItem
{
    public required NotificationChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public required string TemplateKey { get; init; }
    public Dictionary<string, object>? Variables { get; init; }
    public Priority Priority { get; init; } = Priority.Normal;
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Response after batch send operation.
/// </summary>
public record BatchSendResponse
{
    public required Guid BatchId { get; init; }
    public required int TotalCount { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailureCount { get; init; }
    public required List<BatchItemResult> Results { get; init; }
    public required DateTime ProcessedAt { get; init; }
}

/// <summary>
/// Result for individual batch item.
/// </summary>
public record BatchItemResult
{
    public required int Index { get; init; }
    public Guid? NotificationId { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Request to schedule notifications in batch.
/// </summary>
public record BatchScheduleRequest
{
    public required List<BatchScheduleItem> Notifications { get; init; }
    public DateTime? ScheduledFor { get; init; } // Optional global scheduled time
}

/// <summary>
/// Individual scheduled notification item.
/// </summary>
public record BatchScheduleItem
{
    public required NotificationChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public required string TemplateKey { get; init; }
    public Dictionary<string, object>? Variables { get; init; }
    public required DateTime ScheduledFor { get; init; }
    public Priority Priority { get; init; } = Priority.Normal;
}

/// <summary>
/// Response for batch schedule operation.
/// </summary>
public record BatchScheduleResponse
{
    public required Guid BatchId { get; init; }
    public required int TotalCount { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailureCount { get; init; }
    public required List<BatchItemResult> Results { get; init; }
    public required DateTime ProcessedAt { get; init; }
}

/// <summary>
/// Request to get batch status.
/// </summary>
public record BatchStatusResponse
{
    public required Guid BatchId { get; init; }
    public required string Status { get; init; } // Pending, Processing, Completed, Failed
    public required int TotalCount { get; init; }
    public required int ProcessedCount { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailureCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Request to send a notification.
/// </summary>
public record SendNotificationRequest
{
    public NotificationChannel Channel { get; init; }
    public string Recipient { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public Priority Priority { get; init; } = Priority.Normal;
    public ServiceOrigin ServiceOrigin { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public DateTime? ScheduledFor { get; init; }
}

/// <summary>
/// Response after sending a notification.
/// </summary>
public record SendNotificationResponse
{
    public Guid NotificationId { get; init; }
    public NotificationStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Notification details response.
/// </summary>
public record NotificationResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public NotificationChannel Channel { get; init; }
    public string Recipient { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public Priority Priority { get; init; }
    public NotificationStatus Status { get; init; }
    public ServiceOrigin ServiceOrigin { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Paginated list of notifications.
/// </summary>
public record PagedNotificationsResponse
{
    public IEnumerable<NotificationResponse> Items { get; init; } = Array.Empty<NotificationResponse>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Request to update a notification (PATCH operation).
/// </summary>
public record UpdateNotificationRequest
{
    public string? Status { get; init; }
    public string? CancellationReason { get; init; }
}

/// <summary>
/// Request to schedule a notification for future delivery.
/// </summary>
public record ScheduleNotificationRequest : SendNotificationRequest
{
    public new DateTime ScheduledFor { get; init; }
}

/// <summary>
/// Request to cancel a notification.
/// </summary>
public record CancelNotificationRequest
{
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Request to create user notification preferences.
/// </summary>
public record CreatePreferencesRequest
{
    public string UserId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string? Language { get; init; }
    public string? Timezone { get; init; }
    public Dictionary<NotificationChannel, bool>? ChannelPreferences { get; init; }
    public TimeSpan? QuietHoursStart { get; init; }
    public TimeSpan? QuietHoursEnd { get; init; }
    public bool? EnableQuietHours { get; init; }
}

/// <summary>
/// Request to update notification preferences.
/// </summary>
public record UpdatePreferencesRequest
{
    public string? Language { get; init; }
    public string? Timezone { get; init; }
    public Dictionary<NotificationChannel, bool>? ChannelPreferences { get; init; }
    public TimeSpan? QuietHoursStart { get; init; }
    public TimeSpan? QuietHoursEnd { get; init; }
    public bool? EnableQuietHours { get; init; }
}

/// <summary>
/// User notification preferences response.
/// </summary>
public record PreferencesResponse
{
    public Guid UserId { get; init; }
    public Dictionary<NotificationChannel, bool> ChannelPreferences { get; init; } = new();
    public TimeOnly? QuietHoursStart { get; init; }
    public TimeOnly? QuietHoursEnd { get; init; }
    public bool EnableQuietHours { get; init; }
    public DateTime UpdatedAt { get; init; }
}

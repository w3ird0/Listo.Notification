using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents user notification preferences and quiet hours (tenant-scoped).
/// </summary>
public class PreferenceEntity
{
    public Guid PreferenceId { get; set; }

    public Guid TenantId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>
    /// JSON: {enabled, startTime, endTime, timezone}
    /// </summary>
    public string? QuietHours { get; set; }

    /// <summary>
    /// JSON array of enabled topic names
    /// </summary>
    public string? Topics { get; set; }

    public string Locale { get; set; } = "en-US";

    public DateTime UpdatedAt { get; set; }
}

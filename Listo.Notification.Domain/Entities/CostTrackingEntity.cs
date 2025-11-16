using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents per-message cost tracking for budget management (per-tenant and per-service).
/// </summary>
public class CostTrackingEntity
{
    public Guid CostId { get; set; }

    public Guid TenantId { get; set; }

    public ServiceOrigin ServiceOrigin { get; set; }

    public NotificationChannel Channel { get; set; }

    /// <summary>
    /// Provider name: fcm, twilio, sendgrid, aws_sns, acs
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Cost per unit in micros (1/1,000,000)
    /// </summary>
    public long UnitCostMicros { get; set; }

    /// <summary>
    /// Currency code: USD, EUR, etc.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Reference to Notifications table
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// Number of units (segments for SMS)
    /// </summary>
    public int UsageUnits { get; set; }

    /// <summary>
    /// UsageUnits * UnitCostMicros
    /// </summary>
    public long TotalCostMicros { get; set; }

    public DateTime OccurredAt { get; set; }
}

using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents an immutable audit trail entry for compliance (GDPR, SOC 2).
/// </summary>
public class AuditLogEntity
{
    public Guid AuditId { get; set; }

    /// <summary>
    /// Action: created, updated, deleted, sent, delivered
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Entity type: notification, template, preference, user
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>
    /// User who performed action (null for system actions)
    /// </summary>
    public string? UserId { get; set; }

    public ServiceOrigin? ServiceOrigin { get; set; }

    public ActorType ActorType { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>
    /// Entity state before change (JSON)
    /// </summary>
    public string? BeforeJson { get; set; }

    /// <summary>
    /// Entity state after change (JSON)
    /// </summary>
    public string? AfterJson { get; set; }

    public DateTime OccurredAt { get; set; }
}

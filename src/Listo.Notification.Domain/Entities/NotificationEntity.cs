using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents an immutable audit log of a sent notification.
/// This table serves as the historical record for compliance and analytics.
/// </summary>
public class NotificationEntity
{
    /// <summary>
    /// Unique notification identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant identifier for multi-tenancy isolation
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Target user ID (null for broadcast notifications)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Service that originated this notification
    /// </summary>
    public ServiceOrigin ServiceOrigin { get; set; }

    /// <summary>
    /// Delivery channel for this notification
    /// </summary>
    public NotificationChannel Channel { get; set; }

    /// <summary>
    /// Template key used for rendering
    /// </summary>
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// Current delivery status
    /// </summary>
    public NotificationStatus Status { get; set; }

    /// <summary>
    /// Priority level for delivery
    /// </summary>
    public Priority Priority { get; set; }

    /// <summary>
    /// Recipient address (email, phone, or device token)
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// Notification subject/title
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Notification body/content
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// JSON metadata for additional context
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Scheduled delivery time (null for immediate delivery)
    /// </summary>
    public DateTime? ScheduledFor { get; set; }

    /// <summary>
    /// Scheduled delivery time for database
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Actual send timestamp
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Delivery confirmation timestamp
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// When the notification was read by the user
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// External provider message ID (e.g., Twilio SID, SendGrid message ID)
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Provider error code if delivery failed
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Detailed error message if delivery failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Record creation timestamp (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp (UTC)
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

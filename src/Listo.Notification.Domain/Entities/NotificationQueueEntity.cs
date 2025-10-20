using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents a queued notification pending delivery with encrypted PII.
/// </summary>
public class NotificationQueueEntity
{
    public Guid QueueId { get; set; }

    public string? UserId { get; set; }

    public ServiceOrigin ServiceOrigin { get; set; }

    public NotificationChannel Channel { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload containing template variables and metadata
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// AES-256-GCM encrypted email stored as Base64 (value object handles IV)
    /// </summary>
    public string? EncryptedEmail { get; set; }

    /// <summary>
    /// AES-256-GCM encrypted phone number stored as Base64
    /// </summary>
    public string? EncryptedPhoneNumber { get; set; }

    /// <summary>
    /// AES-256-GCM encrypted FCM token stored as Base64
    /// </summary>
    public string? EncryptedFirebaseToken { get; set; }

    public string? EmailHash { get; set; }

    public string? PhoneHash { get; set; }

    public string PreferredLocale { get; set; } = "en-US";

    public DateTime? ScheduledAt { get; set; }

    public int Attempts { get; set; }

    public DateTime? NextAttemptAt { get; set; }

    public string? LastErrorCode { get; set; }

    public string? LastErrorMessage { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

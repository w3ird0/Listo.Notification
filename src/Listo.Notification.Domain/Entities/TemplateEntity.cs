using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Represents a notification template with versioning and localization support.
/// </summary>
public class TemplateEntity
{
    public Guid TemplateId { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; }

    public string Locale { get; set; } = "en-US";

    /// <summary>
    /// Email subject or push notification title
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Template body with {{variable}} placeholders (Scriban syntax)
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of expected variable names
    /// </summary>
    public string Variables { get; set; } = "[]";

    public int Version { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

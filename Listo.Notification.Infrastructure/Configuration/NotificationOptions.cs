using System.ComponentModel.DataAnnotations;

namespace Listo.Notification.Infrastructure.Configuration;

/// <summary>
/// General notification service configuration options.
/// </summary>
public class NotificationOptions
{
    public const string SectionName = "Notification";

    /// <summary>
    /// Service name for internal identification
    /// </summary>
    [Required]
    public string ServiceName { get; set; } = "Listo.Notification";

    /// <summary>
    /// Default notification retention period in days
    /// </summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Maximum batch size for batch operations
    /// </summary>
    [Range(1, 1000)]
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Enable real-time delivery via SignalR
    /// </summary>
    public bool EnableRealTimeDelivery { get; set; } = true;

    /// <summary>
    /// Enable async processing via Service Bus
    /// </summary>
    public bool EnableAsyncProcessing { get; set; } = true;

    /// <summary>
    /// Default notification priority
    /// </summary>
    public string DefaultPriority { get; set; } = "Normal";

    /// <summary>
    /// Maximum attachment size in bytes (default 10MB)
    /// </summary>
    [Range(1, 104857600)] // 1 byte to 100MB
    public long MaxAttachmentSizeBytes { get; set; } = 10485760; // 10MB

    /// <summary>
    /// Allowed attachment MIME types
    /// </summary>
    public List<string> AllowedAttachmentTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "application/pdf",
        "text/plain"
    };
}

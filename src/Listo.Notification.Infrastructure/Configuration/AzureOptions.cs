using System.ComponentModel.DataAnnotations;

namespace Listo.Notification.Infrastructure.Configuration;

/// <summary>
/// Azure Service Bus configuration options.
/// </summary>
public class ServiceBusOptions
{
    public const string SectionName = "Azure:ServiceBus";

    /// <summary>
    /// Service Bus connection string (use Key Vault reference in production)
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Notification queue name
    /// </summary>
    [Required]
    public string NotificationQueueName { get; set; } = "notifications";

    /// <summary>
    /// Failed notification queue name
    /// </summary>
    [Required]
    public string FailedNotificationQueueName { get; set; } = "notifications-failed";

    /// <summary>
    /// Topic name for pub/sub events
    /// </summary>
    [Required]
    public string NotificationTopicName { get; set; } = "notification-events";

    /// <summary>
    /// Maximum delivery attempts before moving to dead letter queue
    /// </summary>
    [Range(1, 100)]
    public int MaxDeliveryCount { get; set; } = 10;

    /// <summary>
    /// Message lock duration in seconds
    /// </summary>
    [Range(5, 300)]
    public int LockDurationSeconds { get; set; } = 60;
}

/// <summary>
/// Azure SignalR Service configuration options.
/// </summary>
public class SignalROptions
{
    public const string SectionName = "Azure:SignalR";

    /// <summary>
    /// Azure SignalR Service connection string (use Key Vault reference in production)
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Use Azure SignalR Service (true) or self-hosted with Redis backplane (false)
    /// </summary>
    public bool UseAzureSignalR { get; set; } = false;

    /// <summary>
    /// Service mode: Default, Serverless, or Classic
    /// </summary>
    public string ServiceMode { get; set; } = "Default";

    /// <summary>
    /// Maximum number of connections per hub
    /// </summary>
    [Range(1, 100000)]
    public int MaxConnectionsPerHub { get; set; } = 10000;
}

/// <summary>
/// Azure Blob Storage configuration for attachments.
/// </summary>
public class BlobStorageOptions
{
    public const string SectionName = "Azure:BlobStorage";

    /// <summary>
    /// Storage account connection string (use Key Vault reference in production)
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for notification attachments
    /// </summary>
    [Required]
    public string ContainerName { get; set; } = "notification-attachments";

    /// <summary>
    /// SAS token expiration time in hours
    /// </summary>
    [Range(1, 168)] // 1 hour to 7 days
    public int SasExpirationHours { get; set; } = 24;

    /// <summary>
    /// Enable CDN for blob URLs
    /// </summary>
    public bool EnableCdn { get; set; } = false;

    /// <summary>
    /// CDN endpoint URL (if enabled)
    /// </summary>
    public string? CdnEndpoint { get; set; }
}

/// <summary>
/// Azure Key Vault configuration.
/// </summary>
public class KeyVaultOptions
{
    public const string SectionName = "Azure:KeyVault";

    /// <summary>
    /// Key Vault URI
    /// </summary>
    public string? VaultUri { get; set; }

    /// <summary>
    /// Tenant ID for Azure AD authentication
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Client ID for managed identity or service principal
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Use managed identity (recommended for production)
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Client secret (only if not using managed identity)
    /// </summary>
    public string? ClientSecret { get; set; }
}

/// <summary>
/// Application Insights configuration.
/// </summary>
public class ApplicationInsightsOptions
{
    public const string SectionName = "Azure:ApplicationInsights";

    /// <summary>
    /// Application Insights connection string
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Instrumentation key (legacy, prefer ConnectionString)
    /// </summary>
    public string? InstrumentationKey { get; set; }

    /// <summary>
    /// Enable adaptive sampling
    /// </summary>
    public bool EnableAdaptiveSampling { get; set; } = true;

    /// <summary>
    /// Sampling percentage (0.1 to 100)
    /// </summary>
    [Range(0.1, 100)]
    public double SamplingPercentage { get; set; } = 100.0;

    /// <summary>
    /// Enable dependency tracking
    /// </summary>
    public bool EnableDependencyTracking { get; set; } = true;
}

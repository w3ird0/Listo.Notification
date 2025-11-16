namespace Listo.Notification.Infrastructure.Configuration;

/// <summary>
/// Feature flags for enabling/disabling features at runtime.
/// </summary>
public class FeatureFlags
{
    public const string SectionName = "FeatureFlags";

    /// <summary>
    /// Enable SMS notifications
    /// </summary>
    public bool EnableSms { get; set; } = true;

    /// <summary>
    /// Enable email notifications
    /// </summary>
    public bool EnableEmail { get; set; } = true;

    /// <summary>
    /// Enable push notifications
    /// </summary>
    public bool EnablePush { get; set; } = true;

    /// <summary>
    /// Enable in-app notifications
    /// </summary>
    public bool EnableInApp { get; set; } = true;

    /// <summary>
    /// Enable template rendering and management
    /// </summary>
    public bool EnableTemplates { get; set; } = true;

    /// <summary>
    /// Enable batch operations
    /// </summary>
    public bool EnableBatchOperations { get; set; } = true;

    /// <summary>
    /// Enable scheduled notifications
    /// </summary>
    public bool EnableScheduledNotifications { get; set; } = true;

    /// <summary>
    /// Enable user preferences management
    /// </summary>
    public bool EnableUserPreferences { get; set; } = true;

    /// <summary>
    /// Enable cost tracking and budget enforcement
    /// </summary>
    public bool EnableCostManagement { get; set; } = true;

    /// <summary>
    /// Enable rate limiting
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Enable webhook callbacks from providers (Twilio, SendGrid, FCM)
    /// </summary>
    public bool EnableWebhooks { get; set; } = true;

    /// <summary>
    /// Enable SignalR real-time messaging
    /// </summary>
    public bool EnableSignalR { get; set; } = true;

    /// <summary>
    /// Enable in-app messaging (conversations and messages)
    /// </summary>
    public bool EnableInAppMessaging { get; set; } = true;

    /// <summary>
    /// Enable attachment uploads
    /// </summary>
    public bool EnableAttachments { get; set; } = true;

    /// <summary>
    /// Enable audit logging
    /// </summary>
    public bool EnableAuditLog { get; set; } = true;

    /// <summary>
    /// Enable Service Bus integration for async processing
    /// </summary>
    public bool EnableServiceBus { get; set; } = true;

    /// <summary>
    /// Enable retry mechanisms for failed notifications
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Enable provider failover (secondary SMS/Email providers)
    /// </summary>
    public bool EnableProviderFailover { get; set; } = true;

    /// <summary>
    /// Enable detailed error logging (may expose sensitive info, disable in production)
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

    /// <summary>
    /// Enable Swagger UI (disable in production)
    /// </summary>
    public bool EnableSwagger { get; set; } = true;

    /// <summary>
    /// Enable Application Insights telemetry
    /// </summary>
    public bool EnableApplicationInsights { get; set; } = true;

    /// <summary>
    /// Enable health check endpoints
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;
}

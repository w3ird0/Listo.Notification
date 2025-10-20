using System.ComponentModel.DataAnnotations;

namespace Listo.Notification.Infrastructure.Configuration;

/// <summary>
/// Redis configuration options.
/// </summary>
public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection string
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Database number (0-15 for default Redis configuration)
    /// </summary>
    [Range(0, 15)]
    public int Database { get; set; } = 0;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    [Range(1000, 30000)]
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Sync timeout in milliseconds
    /// </summary>
    [Range(1000, 30000)]
    public int SyncTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Enable SSL/TLS
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Password for Redis authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Allow admin commands (not recommended for production)
    /// </summary>
    public bool AllowAdmin { get; set; } = false;

    /// <summary>
    /// Abort connection on connect failure
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>
    /// Key prefix for multi-tenancy or environment separation
    /// </summary>
    public string KeyPrefix { get; set; } = "listo:notification:";

    /// <summary>
    /// Default TTL for cache entries in seconds
    /// </summary>
    [Range(60, 2592000)] // 1 minute to 30 days
    public int DefaultTtlSeconds { get; set; } = 3600; // 1 hour
}

/// <summary>
/// Rate limiting configuration options.
/// </summary>
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Enable rate limiting globally
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default rate limit for SMS (per user per minute)
    /// </summary>
    [Range(1, 1000)]
    public int SmsPerMinute { get; set; } = 5;

    /// <summary>
    /// Default rate limit for Email (per user per minute)
    /// </summary>
    [Range(1, 1000)]
    public int EmailPerMinute { get; set; } = 10;

    /// <summary>
    /// Default rate limit for Push notifications (per user per minute)
    /// </summary>
    [Range(1, 1000)]
    public int PushPerMinute { get; set; } = 20;

    /// <summary>
    /// Default rate limit for In-App notifications (per user per minute)
    /// </summary>
    [Range(1, 1000)]
    public int InAppPerMinute { get; set; } = 50;

    /// <summary>
    /// Burst capacity multiplier (allows temporary burst above limit)
    /// </summary>
    [Range(1.0, 10.0)]
    public double BurstMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Service-to-service rate limit (per service per minute)
    /// </summary>
    [Range(100, 10000)]
    public int ServicePerMinute { get; set; } = 1000;

    /// <summary>
    /// Rate limit window in seconds
    /// </summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Enable 429 response headers (Retry-After, X-RateLimit-*)
    /// </summary>
    public bool EnableResponseHeaders { get; set; } = true;
}

/// <summary>
/// Cost management and budget configuration.
/// </summary>
public class CostManagementOptions
{
    public const string SectionName = "CostManagement";

    /// <summary>
    /// Enable cost tracking and budget enforcement
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default monthly budget in USD per tenant
    /// </summary>
    [Range(0.01, 1000000.0)]
    public decimal DefaultMonthlyBudgetUsd { get; set; } = 100.0m;

    /// <summary>
    /// Send alert when budget reaches this percentage (0.0 to 1.0)
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal AlertThreshold80Percent { get; set; } = 0.80m;

    /// <summary>
    /// Send critical alert when budget reaches this percentage (0.0 to 1.0)
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal AlertThreshold100Percent { get; set; } = 1.0m;

    /// <summary>
    /// Allow high-priority notifications when budget is exceeded
    /// </summary>
    public bool AllowHighPriorityWhenExceeded { get; set; } = true;

    /// <summary>
    /// Cost per SMS in USD
    /// </summary>
    [Range(0.0001, 1.0)]
    public decimal SmsCostUsd { get; set; } = 0.0075m;

    /// <summary>
    /// Cost per Email in USD
    /// </summary>
    [Range(0.0001, 1.0)]
    public decimal EmailCostUsd { get; set; } = 0.001m;

    /// <summary>
    /// Cost per Push notification in USD
    /// </summary>
    [Range(0.0001, 1.0)]
    public decimal PushCostUsd { get; set; } = 0.0005m;

    /// <summary>
    /// Cost per In-App notification in USD
    /// </summary>
    [Range(0.0001, 1.0)]
    public decimal InAppCostUsd { get; set; } = 0.0001m;

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; set; } = "USD";
}

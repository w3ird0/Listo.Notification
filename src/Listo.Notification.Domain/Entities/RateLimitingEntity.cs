namespace Listo.Notification.Domain.Entities;

/// <summary>
/// Configurable rate limits per service and user with tenant-scoped maximum caps.
/// Enforces quotas using Redis token bucket pattern to prevent abuse and manage costs.
/// </summary>
public class RateLimitingEntity
{
    /// <summary>
    /// Unique configuration identifier
    /// </summary>
    public Guid ConfigId { get; set; }

    /// <summary>
    /// Tenant ID (null for global defaults)
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Service origin (auth, orders, ridesharing, products, * for wildcard)
    /// </summary>
    public string ServiceOrigin { get; set; } = string.Empty;

    /// <summary>
    /// Channel (push, sms, email, inApp, * for wildcard)
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Time window for per-user limit in seconds (default: 3600 = 1 hour)
    /// </summary>
    public int PerUserWindowSeconds { get; set; } = 3600;

    /// <summary>
    /// Maximum notifications per user in window (default: 60)
    /// </summary>
    public int PerUserMax { get; set; } = 60;

    /// <summary>
    /// Absolute maximum cap per user (cannot be exceeded, even with burst)
    /// </summary>
    public int PerUserMaxCap { get; set; } = 100;

    /// <summary>
    /// Time window for per-service limit in seconds (default: 86400 = 1 day)
    /// </summary>
    public int PerServiceWindowSeconds { get; set; } = 86400;

    /// <summary>
    /// Maximum notifications per service in window (default: 50000)
    /// </summary>
    public int PerServiceMax { get; set; } = 50000;

    /// <summary>
    /// Absolute maximum cap for service (cannot be exceeded)
    /// </summary>
    public int PerServiceMaxCap { get; set; } = 75000;

    /// <summary>
    /// Allowed burst above limit (default: 20)
    /// </summary>
    public int BurstSize { get; set; } = 20;

    /// <summary>
    /// Is this limit enforced (default: true)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Redis key for per-user rate limiting
    /// </summary>
    public string GetUserRateLimitKey(Guid tenantId, string userId) 
        => $"ratelimit:tenant:{tenantId}:user:{userId}:channel:{Channel}";

    /// <summary>
    /// Redis key for per-service rate limiting
    /// </summary>
    public string GetServiceRateLimitKey(Guid tenantId) 
        => $"ratelimit:tenant:{tenantId}:service:{ServiceOrigin}:channel:{Channel}";
}

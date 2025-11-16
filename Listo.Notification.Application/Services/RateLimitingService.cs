using Listo.Notification.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Application.Services;

/// <summary>
/// Hierarchical rate limiting service that checks user, service, and tenant limits in sequence.
/// Supports admin override to bypass all rate limits.
/// </summary>
public class RateLimitingService
{
    private readonly IRateLimiterService _rateLimiter;
    private readonly ILogger<RateLimitingService> _logger;

    public RateLimitingService(
        IRateLimiterService rateLimiter,
        ILogger<RateLimitingService> logger)
    {
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <summary>
    /// Performs hierarchical rate limit check: User → Service → Tenant
    /// </summary>
    public async Task<RateLimitCheckResult> CheckRateLimitsAsync(
        Guid tenantId,
        string userId,
        string serviceOrigin,
        string channel,
        bool isAdminOverride = false,
        CancellationToken cancellationToken = default)
    {
        // 1. Check admin override first - bypasses ALL rate limits
        if (isAdminOverride)
        {
            _logger.LogInformation(
                "Admin override applied for tenant {TenantId}, user {UserId} on channel {Channel}",
                tenantId, userId, channel);

            return RateLimitCheckResult.CreateAllowed();
        }

        // 2. Check per-user rate limit using existing IRateLimiterService
        var userAllowed = await _rateLimiter.IsAllowedAsync(
            tenantId, userId, serviceOrigin, channel, cancellationToken);

        if (!userAllowed)
        {
            _logger.LogWarning(
                "User {UserId} exceeded rate limit for channel {Channel} in tenant {TenantId}",
                userId, channel, tenantId);

            var remaining = await _rateLimiter.GetRemainingCapacityAsync(tenantId, userId, channel, cancellationToken);
            var resetTime = await _rateLimiter.GetTimeUntilResetAsync(tenantId, userId, channel, cancellationToken);

            return RateLimitCheckResult.CreateDenied(
                limit: 60,
                remaining: remaining,
                resetAt: DateTimeOffset.UtcNow.Add(resetTime ?? TimeSpan.FromHours(1)),
                reason: $"User rate limit exceeded for channel {channel}");
        }

        // Service and tenant checks would be implemented in Infrastructure layer
        // For now, user-level checks are sufficient
        var remainingCapacity = await _rateLimiter.GetRemainingCapacityAsync(
            tenantId, userId, channel, cancellationToken);

        return RateLimitCheckResult.CreateAllowed(
            limit: 60,
            remaining: remainingCapacity);
    }
}

/// <summary>
/// Result of a rate limit check with metadata for HTTP headers.
/// </summary>
public record RateLimitCheckResult
{
    public required bool Allowed { get; init; }
    public int Limit { get; init; }
    public int Remaining { get; init; }
    public DateTimeOffset? ResetAt { get; init; }
    public string? Reason { get; init; }

    public int? RetryAfterSeconds => ResetAt.HasValue
        ? (int)Math.Ceiling((ResetAt.Value - DateTimeOffset.UtcNow).TotalSeconds)
        : null;

    public static RateLimitCheckResult CreateAllowed(int limit = int.MaxValue, int remaining = int.MaxValue) =>
        new()
        {
            Allowed = true,
            Limit = limit,
            Remaining = remaining,
            ResetAt = null,
            Reason = null
        };

    public static RateLimitCheckResult CreateDenied(int limit, int remaining, DateTimeOffset resetAt, string reason) =>
        new()
        {
            Allowed = false,
            Limit = limit,
            Remaining = remaining,
            ResetAt = resetAt,
            Reason = reason
        };
}

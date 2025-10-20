using Listo.Notification.Application.Services;
using System.Security.Claims;

namespace Listo.Notification.API.Middleware;

/// <summary>
/// Middleware for hierarchical rate limiting with admin override support.
/// Adds rate limit headers to all responses and returns 429 when limits exceeded.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        RateLimitingService rateLimitingService)
    {
        // Skip rate limiting for health checks and internal endpoints
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/api/v1/internal"))
        {
            await _next(context);
            return;
        }

        // Extract tenant and user from JWT claims
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        // If no tenant or user, skip rate limiting (will be handled by auth middleware)
        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        // Check for admin override header
        var hasAdminOverride = context.Request.Headers.TryGetValue("X-Admin-Override", out var overrideValue)
            && string.Equals(overrideValue.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        // Validate admin override scope if present
        var isAdminOverride = false;
        if (hasAdminOverride)
        {
            var hasAdminScope = context.User.HasClaim("scope", "notifications:admin")
                || context.User.IsInRole("Admin");

            if (hasAdminScope)
            {
                isAdminOverride = true;
                _logger.LogInformation(
                    "Admin override applied for user {UserId} in tenant {TenantId}",
                    userId, tenantId);
            }
            else
            {
                _logger.LogWarning(
                    "Admin override attempted without valid scope for user {UserId}",
                    userId);
            }
        }

        // Extract service origin and channel from request
        var serviceOrigin = context.Request.Headers["X-Service-Origin"].ToString() ?? "api";
        var channel = ExtractChannelFromPath(context.Request.Path);

        // Perform hierarchical rate limit check
        var result = await rateLimitingService.CheckRateLimitsAsync(
            tenantId: Guid.Parse(tenantId),
            userId: userId,
            serviceOrigin: serviceOrigin,
            channel: channel,
            isAdminOverride: isAdminOverride,
            cancellationToken: context.RequestAborted);

        // Add rate limit headers to response
        context.Response.Headers.Append("X-RateLimit-Limit", result.Limit.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining", result.Remaining.ToString());

        if (result.ResetAt.HasValue)
        {
            context.Response.Headers.Append(
                "X-RateLimit-Reset",
                result.ResetAt.Value.ToUnixTimeSeconds().ToString());
        }

        // If not allowed, return 429 Too Many Requests
        if (!result.Allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (result.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers.Append(
                    "Retry-After",
                    result.RetryAfterSeconds.Value.ToString());
            }

            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "RATE_LIMIT_EXCEEDED",
                    message = result.Reason ?? "Rate limit exceeded. Please retry after the specified time.",
                    retryAfter = result.RetryAfterSeconds,
                    limit = result.Limit,
                    remaining = result.Remaining
                }
            });

            _logger.LogWarning(
                "Rate limit exceeded for user {UserId} in tenant {TenantId}: {Reason}",
                userId, tenantId, result.Reason);

            return;
        }

        // Rate limit check passed - continue to next middleware
        await _next(context);
    }

    private static string ExtractChannelFromPath(PathString path)
    {
        // Extract channel from path like /api/v1/notifications/send or /api/v1/sms/send
        var segments = path.Value?.Split('/') ?? Array.Empty<string>();

        if (segments.Length >= 4)
        {
            var potentialChannel = segments[3].ToLowerInvariant();

            return potentialChannel switch
            {
                "sms" => "sms",
                "email" => "email",
                "push" => "push",
                "messaging" or "conversations" => "in-app",
                _ => "api"
            };
        }

        return "api";
    }
}

/// <summary>
/// Extension method to register rate limiting middleware.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}

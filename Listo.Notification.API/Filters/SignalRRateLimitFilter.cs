using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Listo.Notification.API.Filters;

/// <summary>
/// SignalR hub filter for rate limiting messages and typing indicators.
/// NOTE: Rate limiting implementation deferred - integrate with RedisTokenBucketLimiter
/// in production deployment.
/// </summary>
public class SignalRRateLimitFilter : IHubFilter
{
    private readonly ILogger<SignalRRateLimitFilter> _logger;

    // Rate limits per user (for documentation)
    private const int MessagesPerMinute = 60;
    private const int TypingIndicatorsPerMinute = 10;

    public SignalRRateLimitFilter(ILogger<SignalRRateLimitFilter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var userId = invocationContext.Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = invocationContext.Context.User?.FindFirst("tenant_id")?.Value;
        var methodName = invocationContext.HubMethodName;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            return await next(invocationContext);
        }

        // TODO: Implement rate limiting for SignalR methods in production
        // Integrate with RedisTokenBucketLimiter service to enforce:
        // - SendMessage: 60 messages/min per user
        // - StartTyping: 10 indicators/min per user
        // Throttle violations without disconnecting the client
        
        _logger.LogDebug(
            "SignalR method invoked: Method={Method}, UserId={UserId}, TenantId={TenantId}",
            methodName, userId, tenantId);

        return await next(invocationContext);
    }
}

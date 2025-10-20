using System.Security.Claims;

namespace Listo.Notification.API.Middleware;

/// <summary>
/// Middleware that extracts tenant information from JWT claims and makes it available via HttpContext.
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract tenant ID from JWT claims
        var tenantIdClaim = context.User?.FindFirst("tenantId")?.Value;
        
        if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            // Store tenant ID in HttpContext items for easy access
            context.Items["TenantId"] = tenantId;
            
            _logger.LogDebug("Tenant context set: TenantId={TenantId}", tenantId);
        }
        else if (context.User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogWarning("Authenticated request missing valid tenantId claim");
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering tenant context middleware.
/// </summary>
public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}

/// <summary>
/// Helper class to access tenant context from HttpContext.
/// </summary>
public static class TenantContext
{
    /// <summary>
    /// Gets the current tenant ID from HttpContext, if available.
    /// </summary>
    public static Guid? GetTenantId(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is Guid tenantId)
        {
            return tenantId;
        }
        
        return null;
    }

    /// <summary>
    /// Gets the current tenant ID, throwing an exception if not found.
    /// </summary>
    public static Guid GetRequiredTenantId(HttpContext httpContext)
    {
        var tenantId = GetTenantId(httpContext);
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant context is required but not available");
        }
        
        return tenantId.Value;
    }
}

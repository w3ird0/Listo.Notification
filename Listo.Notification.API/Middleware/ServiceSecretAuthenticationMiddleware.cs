using System.Security.Claims;

namespace Listo.Notification.API.Middleware;

/// <summary>
/// Middleware for authenticating service-to-service requests using X-Service-Secret header.
/// Validates shared secrets from Azure Key Vault and sets service principal claims.
/// </summary>
public class ServiceSecretAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ServiceSecretAuthenticationMiddleware> _logger;
    private const string ServiceSecretHeader = "X-Service-Secret";
    private const string ServiceOriginHeader = "X-Service-Origin";

    public ServiceSecretAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ServiceSecretAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        // Check if request is for internal/service endpoints
        if (!context.Request.Path.StartsWithSegments("/api/v1/internal"))
        {
            await _next(context);
            return;
        }

        // Skip if already authenticated via JWT
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // Extract service secret header
        if (!context.Request.Headers.TryGetValue(ServiceSecretHeader, out var secretHeader))
        {
            _logger.LogWarning(
                "Missing {Header} header for internal endpoint: {Path}",
                ServiceSecretHeader,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = $"{ServiceSecretHeader} header is required for internal endpoints"
            });
            return;
        }

        // Extract service origin header
        if (!context.Request.Headers.TryGetValue(ServiceOriginHeader, out var serviceOrigin))
        {
            _logger.LogWarning(
                "Missing {Header} header for internal endpoint: {Path}",
                ServiceOriginHeader,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = $"{ServiceOriginHeader} header is required for internal endpoints"
            });
            return;
        }

        var providedSecret = secretHeader.ToString();
        var service = serviceOrigin.ToString().ToLowerInvariant();

        // Validate service secret
        var expectedSecret = service switch
        {
            "auth" => configuration["ServiceSecrets:Auth"],
            "orders" => configuration["ServiceSecrets:Orders"],
            "ridesharing" => configuration["ServiceSecrets:RideSharing"],
            "products" => configuration["ServiceSecrets:Products"],
            _ => null
        };

        if (string.IsNullOrEmpty(expectedSecret))
        {
            _logger.LogWarning(
                "Unknown service origin: {ServiceOrigin}",
                service);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = $"Unknown service origin: {service}"
            });
            return;
        }

        if (providedSecret != expectedSecret)
        {
            _logger.LogWarning(
                "Invalid service secret for service: {ServiceOrigin}, Path: {Path}",
                service,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "Invalid service secret"
            });
            return;
        }

        // Create service principal claims
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, $"service:{service}"),
            new Claim(ClaimTypes.Role, "Service"),
            new Claim("service_origin", service),
            new Claim("auth_method", "service-secret")
        };

        var identity = new ClaimsIdentity(claims, "ServiceSecret");
        context.User = new ClaimsPrincipal(identity);

        _logger.LogInformation(
            "Service authenticated: {ServiceOrigin}, Path: {Path}",
            service,
            context.Request.Path);

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering service secret authentication middleware.
/// </summary>
public static class ServiceSecretAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseServiceSecretAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ServiceSecretAuthenticationMiddleware>();
    }
}

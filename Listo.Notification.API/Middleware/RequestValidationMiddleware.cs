namespace Listo.Notification.API.Middleware;

/// <summary>
/// Middleware for validating incoming requests for security and data integrity.
/// Enforces content-type requirements, request size limits, and header validation.
/// </summary>
public class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;
    private const long MaxRequestBodySize = 5 * 1024 * 1024; // 5 MB
    private const int MaxHeaderValueLength = 8192; // 8 KB

    private static readonly string[] AllowedContentTypes = new[]
    {
        "application/json",
        "application/x-www-form-urlencoded",
        "multipart/form-data"
    };

    public RequestValidationMiddleware(
        RequestDelegate next,
        ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation for GET, HEAD, OPTIONS requests
        if (HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Validate Content-Type header
        if (!string.IsNullOrEmpty(context.Request.ContentType))
        {
            var contentType = context.Request.ContentType.Split(';')[0].Trim();
            if (!AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Invalid Content-Type: {ContentType}, Path: {Path}",
                    contentType,
                    context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "UnsupportedMediaType",
                    message = $"Content-Type '{contentType}' is not supported. Allowed types: {string.Join(", ", AllowedContentTypes)}"
                });
                return;
            }
        }

        // Validate request body size
        if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > MaxRequestBodySize)
        {
            _logger.LogWarning(
                "Request body too large: {Size} bytes, Path: {Path}",
                context.Request.ContentLength.Value,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "PayloadTooLarge",
                message = $"Request body exceeds maximum size of {MaxRequestBodySize / (1024 * 1024)} MB"
            });
            return;
        }

        // Validate header values length (prevent header injection attacks)
        foreach (var header in context.Request.Headers)
        {
            if (header.Value.ToString().Length > MaxHeaderValueLength)
            {
                _logger.LogWarning(
                    "Header value too long: {Header}, Length: {Length}, Path: {Path}",
                    header.Key,
                    header.Value.ToString().Length,
                    context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "BadRequest",
                    message = $"Header '{header.Key}' value exceeds maximum length"
                });
                return;
            }
        }

        // Validate required correlation headers for tracing
        if (!context.Request.Headers.ContainsKey("X-Correlation-Id") &&
            !context.Request.Path.StartsWithSegments("/health") &&
            !context.Request.Path.StartsWithSegments("/swagger"))
        {
            // Auto-generate if missing (don't reject, just log)
            var correlationId = Guid.NewGuid().ToString();
            context.Request.Headers.Append("X-Correlation-Id", correlationId);
            context.Response.Headers.Append("X-Correlation-Id", correlationId);

            _logger.LogDebug(
                "Auto-generated X-Correlation-Id: {CorrelationId}, Path: {Path}",
                correlationId,
                context.Request.Path);
        }
        else if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            // Echo back correlation ID in response
            context.Response.Headers.Append("X-Correlation-Id", correlationId.ToString());
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering request validation middleware.
/// </summary>
public static class RequestValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestValidationMiddleware>();
    }
}

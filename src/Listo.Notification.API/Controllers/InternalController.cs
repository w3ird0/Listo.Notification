using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Internal service-to-service endpoints. Requires X-Service-Secret header authentication.
/// </summary>
[ApiController]
[Route("api/v1/internal")]
[Authorize(Policy = "ServiceOnly")]
public class InternalController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<InternalController> _logger;

    public InternalController(
        INotificationService notificationService,
        ILogger<InternalController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Queue a notification for async processing via Service Bus.
    /// Used by other Listo services (Auth, Orders, RideSharing) to send notifications.
    /// </summary>
    /// <param name="request">Internal notification request with service context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue operation result</returns>
    [HttpPost("notifications/queue")]
    [ProducesResponseType(typeof(QueueNotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<QueueNotificationResponse>> QueueNotification(
        [FromBody] InternalNotificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var serviceName = HttpContext.Items["ServiceName"] as string 
                ?? throw new UnauthorizedAccessException("Service name not found in request context");

            _logger.LogInformation(
                "Queueing notification from service: ServiceName={ServiceName}, Channel={Channel}, EventType={EventType}",
                serviceName, request.Channel, request.EventType);

            var response = await _notificationService.QueueNotificationAsync(
                request,
                serviceName,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid queue notification request");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Queue multiple notifications in a batch for async processing.
    /// Used by other Listo services for efficient bulk notification delivery (e.g. driver broadcasts).
    /// </summary>
    /// <param name="request">Batch notification request with service context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch queue operation result with per-item status</returns>
    [HttpPost("notifications/queue/batch")]
    [ProducesResponseType(typeof(BatchQueueNotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BatchQueueNotificationResponse>> QueueBatchNotifications(
        [FromBody] BatchInternalNotificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var serviceName = HttpContext.Items["ServiceName"] as string 
                ?? throw new UnauthorizedAccessException("Service name not found in request context");

            var notifications = request.Notifications.ToList();
            
            if (!notifications.Any())
            {
                return BadRequest(new { error = "Notification list cannot be empty" });
            }

            if (notifications.Count > 100)
            {
                return BadRequest(new { error = "Batch size cannot exceed 100 notifications" });
            }

            _logger.LogInformation(
                "Queuing batch of {Count} notifications from service: {ServiceName}",
                notifications.Count, serviceName);

            var response = await _notificationService.QueueBatchNotificationsAsync(
                request,
                cancellationToken);

            if (response.FailedCount > 0)
            {
                _logger.LogWarning(
                    "Batch partially failed: {Failed}/{Total} notifications failed to queue",
                    response.FailedCount, response.TotalRequested);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch queue request failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Publish a CloudEvents-formatted event for notification processing.
    /// </summary>
    /// <param name="cloudEvent">CloudEvents payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Event publish result</returns>
    [HttpPost("events/publish")]
    [Consumes("application/cloudevents+json")]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PublishEvent(
        [FromBody] object cloudEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var serviceName = HttpContext.Items["ServiceName"] as string 
                ?? throw new UnauthorizedAccessException("Service name not found in request context");

            _logger.LogInformation(
                "Publishing CloudEvent from service: ServiceName={ServiceName}",
                serviceName);

            await _notificationService.ProcessCloudEventAsync(
                cloudEvent,
                serviceName,
                cancellationToken);

            return Accepted(new { message = "Event accepted for processing" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid CloudEvent");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Health check endpoint for internal monitoring.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service health status</returns>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponse>> HealthCheck(
        CancellationToken cancellationToken)
    {
        try
        {
            var health = await _notificationService.GetHealthAsync(cancellationToken);

            var statusCode = health.Status switch
            {
                "Healthy" => StatusCodes.Status200OK,
                "Degraded" => StatusCodes.Status200OK,
                _ => StatusCodes.Status503ServiceUnavailable
            };

            return StatusCode(statusCode, health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new HealthCheckResponse
            {
                Status = "Unhealthy",
                Components = new Dictionary<string, ComponentHealth>(),
                Timestamp = DateTime.UtcNow
            });
        }
    }
}

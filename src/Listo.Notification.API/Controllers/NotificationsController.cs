using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.API.Middleware;
using System.Security.Claims;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Notifications management endpoints.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ILogger<NotificationsController> _logger;
    private readonly INotificationService _notificationService;

    public NotificationsController(
        ILogger<NotificationsController> logger,
        INotificationService notificationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    /// <summary>
    /// Send a notification via specified channel.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("sms")] // Will apply channel-specific rate limit
    [ProducesResponseType(typeof(SendNotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SendNotificationResponse>> SendNotification(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("UserId claim missing"));

        _logger.LogInformation(
            "Sending notification: TenantId={TenantId}, UserId={UserId}, Channel={Channel}",
            tenantId, userId, request.Channel);

        var response = await _notificationService.SendNotificationAsync(
            tenantId,
            userId,
            request,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Get user's notifications with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedNotificationsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedNotificationsResponse>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("UserId claim missing"));

        _logger.LogInformation(
            "Fetching notifications: TenantId={TenantId}, UserId={UserId}, Page={Page}, PageSize={PageSize}",
            tenantId, userId, page, pageSize);

        var response = await _notificationService.GetUserNotificationsAsync(
            tenantId,
            userId,
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Get a specific notification by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationResponse>> GetNotification(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
        
        _logger.LogInformation(
            "Fetching notification: TenantId={TenantId}, NotificationId={NotificationId}",
            tenantId, id);

        var notification = await _notificationService.GetNotificationByIdAsync(tenantId, id, cancellationToken);
        
        return notification != null ? Ok(notification) : NotFound();
    }

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("UserId claim missing"));

        _logger.LogInformation(
            "Marking notification as read: TenantId={TenantId}, UserId={UserId}, NotificationId={NotificationId}",
            tenantId, userId, id);

        var success = await _notificationService.MarkAsReadAsync(tenantId, userId, id, cancellationToken);
        
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Get notification statistics for the user.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStatistics(CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("UserId claim missing"));

        _logger.LogInformation(
            "Fetching notification statistics: TenantId={TenantId}, UserId={UserId}",
            tenantId, userId);

        var stats = await _notificationService.GetUserStatisticsAsync(tenantId, userId, cancellationToken);

        return Ok(stats);
    }
}

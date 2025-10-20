using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Administrative endpoints for managing rate limits, budgets, and failed notifications.
/// Requires AdminOnly authorization policy.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IRateLimiterService _rateLimiter;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        INotificationService notificationService,
        IRateLimiterService rateLimiter,
        ILogger<AdminController> logger)
    {
        _notificationService = notificationService;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <summary>
    /// Get rate limiting configuration for a tenant or user.
    /// </summary>
    [HttpGet("rate-limits")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRateLimits(
        [FromQuery] string? tenantId = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? channel = null,
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = tenantId ?? TenantContext.GetRequiredTenantId(HttpContext).ToString();

        _logger.LogInformation(
            "Admin fetching rate limits: TenantId={TenantId}, UserId={UserId}, Channel={Channel}",
            currentTenantId, userId, channel);

        var config = await _rateLimiter.GetRateLimitConfigAsync(
            currentTenantId,
            userId,
            channel,
            cancellationToken);

        return Ok(config);
    }

    /// <summary>
    /// Update rate limiting configuration.
    /// </summary>
    [HttpPut("rate-limits")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRateLimits(
        [FromBody] UpdateRateLimitRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Admin updating rate limits: TenantId={TenantId}, Channel={Channel}",
            request.TenantId, request.Channel);

        await _rateLimiter.UpdateRateLimitConfigAsync(request, cancellationToken);
        return Ok(new { message = "Rate limit configuration updated successfully" });
    }

    /// <summary>
    /// Get budget information and usage statistics.
    /// </summary>
    [HttpGet("budgets")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBudgets(
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = tenantId != null ? Guid.Parse(tenantId) : TenantContext.GetRequiredTenantId(HttpContext);

        _logger.LogInformation("Admin fetching budgets: TenantId={TenantId}", currentTenantId);

        var budgets = await _notificationService.GetBudgetsAsync(currentTenantId, cancellationToken);
        return Ok(budgets);
    }

    /// <summary>
    /// Update budget configuration.
    /// </summary>
    [HttpPut("budgets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBudget(
        [FromBody] UpdateBudgetRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Admin updating budget: TenantId={TenantId}, MonthlyBudget={MonthlyBudget}",
            request.TenantId, request.MonthlyBudgetUsd);

        await _notificationService.UpdateBudgetAsync(request, cancellationToken);
        return Ok(new { message = "Budget updated successfully" });
    }

    /// <summary>
    /// Get failed notifications for manual review and retry.
    /// </summary>
    [HttpGet("failed-notifications")]
    [ProducesResponseType(typeof(PagedNotificationsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedNotificationsResponse>> GetFailedNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? channel = null,
        [FromQuery] DateTime? failedAfter = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

        _logger.LogInformation(
            "Admin fetching failed notifications: TenantId={TenantId}, Page={Page}",
            tenantId, pageNumber);

        var notifications = await _notificationService.GetFailedNotificationsAsync(
            tenantId,
            pageNumber,
            pageSize,
            channel,
            failedAfter,
            cancellationToken);

        return Ok(notifications);
    }

    /// <summary>
    /// Manually retry a failed notification.
    /// </summary>
    [HttpPost("notifications/{id}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryNotification(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

        _logger.LogInformation(
            "Admin retrying notification: TenantId={TenantId}, NotificationId={NotificationId}",
            tenantId, id);

        var success = await _notificationService.RetryNotificationAsync(tenantId, id, cancellationToken);

        if (!success)
        {
            return NotFound(new { error = $"Notification {id} not found or cannot be retried" });
        }

        return Ok(new { message = "Notification queued for retry" });
    }

    /// <summary>
    /// Get notification statistics and analytics.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] string? tenantId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = tenantId != null ? Guid.Parse(tenantId) : TenantContext.GetRequiredTenantId(HttpContext);

        _logger.LogInformation(
            "Admin fetching statistics: TenantId={TenantId}, Range={Start}-{End}",
            currentTenantId, startDate, endDate);

        var stats = await _notificationService.GetStatisticsAsync(
            currentTenantId,
            startDate,
            endDate,
            cancellationToken);

        return Ok(stats);
    }
}

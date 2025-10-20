using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Handles batch notification operations for sending multiple notifications efficiently.
/// </summary>
[ApiController]
[Route("api/v1/notifications/batch")]
[Authorize]
public class BatchOperationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<BatchOperationsController> _logger;

    public BatchOperationsController(
        INotificationService notificationService,
        ILogger<BatchOperationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Send multiple notifications in a batch.
    /// </summary>
    /// <param name="request">Batch send request with multiple notifications</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation result with individual notification statuses</returns>
    [HttpPost("send")]
    [ProducesResponseType(typeof(BatchSendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BatchSendResponse>> SendBatch(
        [FromBody] BatchSendRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? throw new InvalidOperationException("UserId claim missing"));

            _logger.LogInformation(
                "Processing batch send: TenantId={TenantId}, BatchSize={BatchSize}",
                tenantId, request.Notifications.Count);

            var response = await _notificationService.SendBatchAsync(
                tenantId,
                userId,
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid batch send operation");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Schedule multiple notifications for future delivery in a batch.
    /// </summary>
    /// <param name="request">Batch schedule request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation result</returns>
    [HttpPost("schedule")]
    [ProducesResponseType(typeof(BatchScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchScheduleResponse>> ScheduleBatch(
        [FromBody] BatchScheduleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? throw new InvalidOperationException("UserId claim missing"));

            _logger.LogInformation(
                "Processing batch schedule: TenantId={TenantId}, BatchSize={BatchSize}, ScheduledFor={ScheduledFor}",
                tenantId, request.Notifications.Count, request.ScheduledFor);

            var response = await _notificationService.ScheduleBatchAsync(
                tenantId,
                userId,
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid batch schedule operation");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get the status of a batch operation.
    /// </summary>
    /// <param name="batchId">Batch ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch status with individual notification results</returns>
    [HttpGet("{batchId}/status")]
    [ProducesResponseType(typeof(BatchStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchStatusResponse>> GetBatchStatus(
        string batchId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

            _logger.LogInformation(
                "Fetching batch status: TenantId={TenantId}, BatchId={BatchId}",
                tenantId, batchId);

            var status = await _notificationService.GetBatchStatusAsync(
                tenantId,
                batchId,
                cancellationToken);

            if (status == null)
            {
                return NotFound(new { error = $"Batch {batchId} not found" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch status for {BatchId}", batchId);
            return StatusCode(500, new { error = "An error occurred while retrieving batch status" });
        }
    }
}

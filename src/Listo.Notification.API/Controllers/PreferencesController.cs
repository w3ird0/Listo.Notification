using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Manages user notification preferences including channel preferences and quiet hours.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PreferencesController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<PreferencesController> _logger;

    public PreferencesController(
        INotificationService notificationService,
        ILogger<PreferencesController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's notification preferences.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User preferences</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PreferencesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PreferencesResponse>> GetPreferences(
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? throw new InvalidOperationException("UserId claim missing"));

            _logger.LogInformation(
                "Fetching preferences: TenantId={TenantId}, UserId={UserId}",
                tenantId, userId);

            var preferences = await _notificationService.GetPreferencesAsync(
                tenantId,
                userId,
                cancellationToken);

            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving preferences");
            return StatusCode(500, new { error = "An error occurred while retrieving preferences" });
        }
    }

    /// <summary>
    /// Update user notification preferences (full replacement).
    /// </summary>
    /// <param name="request">Updated preferences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated preferences</returns>
    [HttpPut]
    [ProducesResponseType(typeof(PreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferencesResponse>> UpdatePreferences(
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? throw new InvalidOperationException("UserId claim missing"));

            _logger.LogInformation(
                "Updating preferences: TenantId={TenantId}, UserId={UserId}",
                tenantId, userId);

            var preferences = await _notificationService.UpdatePreferencesAsync(
                tenantId,
                userId,
                request,
                cancellationToken);

            return Ok(preferences);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid preferences update request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preferences");
            return StatusCode(500, new { error = "An error occurred while updating preferences" });
        }
    }

    /// <summary>
    /// Partially update user notification preferences.
    /// </summary>
    /// <param name="request">Partial preferences update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated preferences</returns>
    [HttpPatch]
    [ProducesResponseType(typeof(PreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferencesResponse>> PatchPreferences(
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? throw new InvalidOperationException("UserId claim missing"));

            _logger.LogInformation(
                "Patching preferences: TenantId={TenantId}, UserId={UserId}",
                tenantId, userId);

            var preferences = await _notificationService.PatchPreferencesAsync(
                tenantId,
                userId,
                request,
                cancellationToken);

            return Ok(preferences);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid preferences patch request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error patching preferences");
            return StatusCode(500, new { error = "An error occurred while patching preferences" });
        }
    }
}

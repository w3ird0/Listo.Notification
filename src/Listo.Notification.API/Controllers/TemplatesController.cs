using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Manages notification templates for reusable notification content.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ManageTemplates")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateRenderingService _templateService;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        ITemplateRenderingService templateService,
        ILogger<TemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Get all templates with pagination.
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <param name="channel">Filter by notification channel</param>
    /// <param name="isActive">Filter by active status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of templates</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedTemplatesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedTemplatesResponse>> GetTemplates(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? channel = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

            // Validate pagination
            pageSize = Math.Min(pageSize, 100);
            pageNumber = Math.Max(pageNumber, 1);

            _logger.LogInformation(
                "Fetching templates: TenantId={TenantId}, Page={Page}, PageSize={PageSize}",
                tenantId, pageNumber, pageSize);

            var templates = await _templateService.GetTemplatesAsync(
                tenantId,
                pageNumber,
                pageSize,
                channel,
                isActive,
                cancellationToken);

            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates");
            return StatusCode(500, new { error = "An error occurred while retrieving templates" });
        }
    }

    /// <summary>
    /// Get a specific template by ID.
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Template details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateResponse>> GetTemplate(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

            _logger.LogInformation(
                "Fetching template: TenantId={TenantId}, TemplateId={TemplateId}",
                tenantId, id);

            var template = await _templateService.GetTemplateByIdAsync(
                tenantId,
                id,
                cancellationToken);

            if (template == null)
            {
                return NotFound(new { error = $"Template {id} not found" });
            }

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {TemplateId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the template" });
        }
    }

    /// <summary>
    /// Create a new notification template.
    /// </summary>
    /// <param name="request">Template details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created template</returns>
    [HttpPost]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TemplateResponse>> CreateTemplate(
        [FromBody] CreateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

            _logger.LogInformation(
                "Creating template: TenantId={TenantId}, TemplateKey={TemplateKey}",
                tenantId, request.TemplateKey);

            var template = await _templateService.CreateTemplateAsync(
                tenantId,
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetTemplate),
                new { id = template.Id },
                template);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid template creation request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            return StatusCode(500, new { error = "An error occurred while creating the template" });
        }
    }

    /// <summary>
    /// Update an existing template.
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <param name="request">Updated template details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated template</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateResponse>> UpdateTemplate(
        Guid id,
        [FromBody] UpdateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

            _logger.LogInformation(
                "Updating template: TenantId={TenantId}, TemplateId={TemplateId}",
                tenantId, id);

            var template = await _templateService.UpdateTemplateAsync(
                tenantId,
                id,
                request,
                cancellationToken);

            if (template == null)
            {
                return NotFound(new { error = $"Template {id} not found" });
            }

            return Ok(template);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid template update request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId}", id);
            return StatusCode(500, new { error = "An error occurred while updating the template" });
        }
    }

    /// <summary>
    /// Delete a template.
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTemplate(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

            _logger.LogInformation(
                "Deleting template: TenantId={TenantId}, TemplateId={TemplateId}",
                tenantId, id);

            var deleted = await _templateService.DeleteTemplateAsync(
                tenantId,
                id,
                cancellationToken);

            if (!deleted)
            {
                return NotFound(new { error = $"Template {id} not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the template" });
        }
    }

    /// <summary>
    /// Render a template with variables (for testing purposes).
    /// </summary>
    /// <param name="request">Template rendering request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered template content</returns>
    [HttpPost("render")]
    [ProducesResponseType(typeof(RenderTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RenderTemplateResponse>> RenderTemplate(
        [FromBody] RenderTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

            _logger.LogInformation(
                "Rendering template: TenantId={TenantId}, TemplateKey={TemplateKey}",
                tenantId, request.TemplateKey);

            var rendered = await _templateService.RenderTemplateAsync(
                tenantId,
                request.TemplateKey,
                request.Variables,
                request.Locale ?? "en-US",
                cancellationToken);

            return Ok(rendered);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Template rendering failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering template");
            return StatusCode(500, new { error = "An error occurred while rendering the template" });
        }
    }
}

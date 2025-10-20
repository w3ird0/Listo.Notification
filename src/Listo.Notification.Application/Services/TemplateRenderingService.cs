using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace Listo.Notification.Application.Services;

/// <summary>
/// Template rendering service using Scriban template engine.
/// Supports variable substitution, conditional logic, and loops.
/// </summary>
public class TemplateRenderingService : ITemplateRenderingService
{
    private readonly ILogger<TemplateRenderingService> _logger;
    private readonly ITemplateRepository _templateRepository;
    private readonly Dictionary<string, Template> _compiledTemplates = new();
    private readonly object _cacheLock = new();

    public TemplateRenderingService(
        ILogger<TemplateRenderingService> logger,
        ITemplateRepository templateRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
    }

    public async Task<string> RenderAsync(
        string templateContent,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateContent))
        {
            throw new ArgumentException("Template content cannot be null or empty", nameof(templateContent));
        }

        try
        {
            // Parse and compile the template
            var template = Template.Parse(templateContent);

            if (template.HasErrors)
            {
                var errors = string.Join("; ", template.Messages.Select(m => m.Message));
                _logger.LogError("Template parsing errors: {Errors}", errors);
                throw new TemplateRenderingException($"Template parsing failed: {errors}");
            }

            // Create script object with variables
            var scriptObject = new ScriptObject();
            
            if (variables != null)
            {
                foreach (var kvp in variables)
                {
                    scriptObject[kvp.Key] = kvp.Value;
                }
            }

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            // Render the template (Scriban doesn't support cancellation tokens directly)
            var result = template.Render(context);

            _logger.LogDebug("Template rendered successfully. Length: {Length} characters", result.Length);

            return result;
        }
        catch (Exception ex) when (ex is not TemplateRenderingException)
        {
            _logger.LogError(ex, "Error rendering template");
            throw new TemplateRenderingException("Template rendering failed", ex);
        }
    }

    public async Task<string> RenderWithCachingAsync(
        string templateKey,
        string templateContent,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            throw new ArgumentException("Template key cannot be null or empty", nameof(templateKey));
        }

        // Get or compile template from cache
        Template template;
        lock (_cacheLock)
        {
            if (!_compiledTemplates.TryGetValue(templateKey, out template!))
            {
                template = Template.Parse(templateContent);
                
                if (template.HasErrors)
                {
                    var errors = string.Join("; ", template.Messages.Select(m => m.Message));
                    _logger.LogError("Template parsing errors for key {TemplateKey}: {Errors}", templateKey, errors);
                    throw new TemplateRenderingException($"Template parsing failed: {errors}");
                }

                _compiledTemplates[templateKey] = template;
                _logger.LogDebug("Template compiled and cached: {TemplateKey}", templateKey);
            }
        }

        try
        {
            // Create script object with variables
            var scriptObject = new ScriptObject();
            
            if (variables != null)
            {
                foreach (var kvp in variables)
                {
                    scriptObject[kvp.Key] = kvp.Value;
                }
            }

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            // Render the template (Scriban doesn't support cancellation tokens directly)
            var result = template.Render(context);

            _logger.LogDebug(
                "Template rendered from cache. Key: {TemplateKey}, Length: {Length} characters",
                templateKey, result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering template with key: {TemplateKey}", templateKey);
            throw new TemplateRenderingException($"Template rendering failed for key: {templateKey}", ex);
        }
    }

    public bool ValidateTemplate(string templateContent, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(templateContent))
        {
            errorMessage = "Template content cannot be null or empty";
            return false;
        }

        try
        {
            var template = Template.Parse(templateContent);

            if (template.HasErrors)
            {
                errorMessage = string.Join("; ", template.Messages.Select(m => m.Message));
                return false;
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Template validation exception: {ex.Message}";
            return false;
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            var count = _compiledTemplates.Count;
            _compiledTemplates.Clear();
            _logger.LogInformation("Template cache cleared. Removed {Count} templates", count);
        }
    }

    public void RemoveFromCache(string templateKey)
    {
        lock (_cacheLock)
        {
            if (_compiledTemplates.Remove(templateKey))
            {
                _logger.LogDebug("Template removed from cache: {TemplateKey}", templateKey);
            }
        }
    }

    // Template CRUD Operations
    public async Task<PagedTemplatesResponse> GetTemplatesAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? channel,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _templateRepository.GetTemplatesAsync(
            tenantId,
            pageNumber,
            pageSize,
            channel,
            isActive,
            cancellationToken);

        return new PagedTemplatesResponse
        {
            Items = items.Select(MapToResponse),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<TemplateResponse?> GetTemplateByIdAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByIdAsync(tenantId, templateId, cancellationToken);
        return template != null ? MapToResponse(template) : null;
    }

    public async Task<TemplateResponse> CreateTemplateAsync(
        Guid tenantId,
        CreateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate template syntax
        if (!ValidateTemplate(request.SubjectTemplate, out var subjectError))
        {
            throw new InvalidOperationException($"Invalid subject template: {subjectError}");
        }

        if (!ValidateTemplate(request.BodyTemplate, out var bodyError))
        {
            throw new InvalidOperationException($"Invalid body template: {bodyError}");
        }

        var template = new TemplateEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateKey = request.TemplateKey,
            Channel = request.Channel,
            SubjectTemplate = request.SubjectTemplate,
            BodyTemplate = request.BodyTemplate,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _templateRepository.CreateAsync(template, cancellationToken);
        return MapToResponse(template);
    }

    public async Task<TemplateResponse?> UpdateTemplateAsync(
        Guid tenantId,
        Guid templateId,
        UpdateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByIdAsync(tenantId, templateId, cancellationToken);
        if (template == null)
            return null;

        // Validate templates if provided
        if (request.SubjectTemplate != null)
        {
            if (!ValidateTemplate(request.SubjectTemplate, out var subjectError))
            {
                throw new InvalidOperationException($"Invalid subject template: {subjectError}");
            }
            template.SubjectTemplate = request.SubjectTemplate;
        }

        if (request.BodyTemplate != null)
        {
            if (!ValidateTemplate(request.BodyTemplate, out var bodyError))
            {
                throw new InvalidOperationException($"Invalid body template: {bodyError}");
            }
            template.BodyTemplate = request.BodyTemplate;
        }

        if (request.Description != null)
            template.Description = request.Description;

        if (request.IsActive.HasValue)
            template.IsActive = request.IsActive.Value;

        template.UpdatedAt = DateTime.UtcNow;

        await _templateRepository.UpdateAsync(template, cancellationToken);
        
        // Remove from cache to force recompilation
        RemoveFromCache(template.TemplateKey);

        return MapToResponse(template);
    }

    public async Task<bool> DeleteTemplateAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByIdAsync(tenantId, templateId, cancellationToken);
        if (template == null)
            return false;

        await _templateRepository.DeleteAsync(tenantId, templateId, cancellationToken);
        RemoveFromCache(template.TemplateKey);
        return true;
    }

    public async Task<RenderTemplateResponse> RenderTemplateAsync(
        Guid tenantId,
        string templateKey,
        Dictionary<string, object> variables,
        string locale,
        CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByKeyAsync(tenantId, templateKey, cancellationToken)
            ?? throw new InvalidOperationException($"Template with key '{templateKey}' not found");

        if (!template.IsActive)
        {
            throw new InvalidOperationException($"Template '{templateKey}' is inactive");
        }

        var renderedSubject = await RenderWithCachingAsync(
            $"{templateKey}_subject",
            template.SubjectTemplate,
            variables,
            cancellationToken);

        var renderedBody = await RenderWithCachingAsync(
            $"{templateKey}_body",
            template.BodyTemplate,
            variables,
            cancellationToken);

        return new RenderTemplateResponse
        {
            TemplateKey = templateKey,
            RenderedSubject = renderedSubject,
            RenderedBody = renderedBody,
            Variables = variables,
            Locale = locale,
            RenderedAt = DateTime.UtcNow
        };
    }

    private static TemplateResponse MapToResponse(TemplateEntity entity)
    {
        return new TemplateResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TemplateKey = entity.TemplateKey,
            Channel = entity.Channel,
            SubjectTemplate = entity.SubjectTemplate,
            BodyTemplate = entity.BodyTemplate,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}

/// <summary>
/// Exception thrown when template rendering fails.
/// </summary>
public class TemplateRenderingException : Exception
{
    public TemplateRenderingException(string message) : base(message)
    {
    }

    public TemplateRenderingException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

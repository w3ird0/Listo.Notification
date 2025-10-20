using Listo.Notification.Application.Interfaces;
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
    private readonly Dictionary<string, Template> _compiledTemplates = new();
    private readonly object _cacheLock = new();

    public TemplateRenderingService(ILogger<TemplateRenderingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

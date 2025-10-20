namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Service for rendering notification templates with variable substitution.
/// </summary>
public interface ITemplateRenderingService
{
    /// <summary>
    /// Renders a template with the given variables.
    /// </summary>
    /// <param name="templateContent">The template content to render.</param>
    /// <param name="variables">Variables to substitute in the template.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered template string.</returns>
    Task<string> RenderAsync(
        string templateContent,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template with caching for improved performance.
    /// </summary>
    /// <param name="templateKey">Unique key for caching the compiled template.</param>
    /// <param name="templateContent">The template content to render.</param>
    /// <param name="variables">Variables to substitute in the template.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered template string.</returns>
    Task<string> RenderWithCachingAsync(
        string templateKey,
        string templateContent,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a template for syntax errors.
    /// </summary>
    /// <param name="templateContent">The template content to validate.</param>
    /// <param name="errorMessage">Error message if validation fails.</param>
    /// <returns>True if template is valid, false otherwise.</returns>
    bool ValidateTemplate(string templateContent, out string? errorMessage);

    /// <summary>
    /// Clears all compiled templates from the cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Removes a specific template from the cache.
    /// </summary>
    /// <param name="templateKey">Key of the template to remove.</param>
    void RemoveFromCache(string templateKey);
}

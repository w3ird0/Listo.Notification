using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Request to create a new notification template.
/// </summary>
public record CreateTemplateRequest
{
    public required string TemplateKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required NotificationChannel Channel { get; init; }
    public required string SubjectTemplate { get; init; }
    public required string BodyTemplate { get; init; }
    public string Locale { get; init; } = "en-US";
    public Dictionary<string, string>? DefaultVariables { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Request to update an existing notification template.
/// </summary>
public record UpdateTemplateRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? SubjectTemplate { get; init; }
    public string? BodyTemplate { get; init; }
    public Dictionary<string, string>? DefaultVariables { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Response containing template details.
/// </summary>
public record TemplateResponse
{
    public required Guid Id { get; init; }
    public required string TemplateKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required NotificationChannel Channel { get; init; }
    public required string SubjectTemplate { get; init; }
    public required string BodyTemplate { get; init; }
    public required string Locale { get; init; }
    public Dictionary<string, string>? DefaultVariables { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Paginated list of templates.
/// </summary>
public record PagedTemplatesResponse
{
    public required IEnumerable<TemplateResponse> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Request to render a template with variables.
/// </summary>
public record RenderTemplateRequest
{
    public required string TemplateKey { get; init; }
    public required Dictionary<string, object> Variables { get; init; }
    public string? Locale { get; init; }
}

/// <summary>
/// Response containing rendered template content.
/// </summary>
public record RenderTemplateResponse
{
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required string Locale { get; init; }
}

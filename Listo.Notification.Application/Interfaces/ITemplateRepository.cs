using Listo.Notification.Domain.Entities;

namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Repository for template entity operations with tenant scoping.
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Gets a template by ID within a tenant scope.
    /// </summary>
    Task<TemplateEntity?> GetByIdAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a template by key within a tenant scope.
    /// </summary>
    Task<TemplateEntity?> GetByKeyAsync(Guid tenantId, string templateKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated templates for a tenant.
    /// </summary>
    Task<(IEnumerable<TemplateEntity> Items, int TotalCount)> GetTemplatesAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? channel = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new template.
    /// </summary>
    Task<TemplateEntity> CreateAsync(TemplateEntity template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    Task UpdateAsync(TemplateEntity template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a template.
    /// </summary>
    Task DeleteAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken = default);
}

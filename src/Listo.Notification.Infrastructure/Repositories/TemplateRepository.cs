using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Listo.Notification.Infrastructure.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly NotificationDbContext _db;

    public TemplateRepository(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<TemplateEntity?> GetByIdAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        // Templates are global (not tenant filtered), but we may later scope by tenant metadata if needed
        return await _db.Templates
            .Where(t => t.TemplateId == templateId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TemplateEntity?> GetByKeyAsync(
        Guid tenantId,
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        return await _db.Templates
            .Where(t => t.TemplateKey == templateKey && t.IsActive)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IEnumerable<TemplateEntity> Items, int TotalCount)> GetTemplatesAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? channel = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Templates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(channel))
        {
            query = query.Where(t => t.Channel.ToString() == channel);
        }

        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<TemplateEntity> CreateAsync(
        TemplateEntity template,
        CancellationToken cancellationToken = default)
    {
        _db.Templates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task UpdateAsync(
        TemplateEntity template,
        CancellationToken cancellationToken = default)
    {
        _db.Templates.Update(template);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.Templates.FirstOrDefaultAsync(t => t.TemplateId == templateId, cancellationToken);
        if (existing != null)
        {
            _db.Templates.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
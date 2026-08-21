using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

public class ContractTemplateService : IContractTemplateService
{
    private readonly ApplicationDbContext _db;

    public ContractTemplateService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ContractTemplate>> GetTemplatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.ContractTemplates.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        return await query
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContractTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.ContractTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<ContractTemplate?> GetDefaultTemplateAsync(CancellationToken cancellationToken = default)
        => await _db.ContractTemplates
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsDefault)
            .OrderBy(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ContractTemplate> CreateTemplateAsync(string name, string htmlTemplate, bool isDefault, Guid? actingUserId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(htmlTemplate))
            throw new ArgumentException("Template HTML is required.", nameof(htmlTemplate));

        var now = DateTime.UtcNow;
        var template = new ContractTemplate
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            HtmlTemplate = htmlTemplate.Trim(),
            IsActive = true,
            IsDefault = isDefault,
            CreatedBy = actingUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (isDefault)
                await ClearDefaultFlagAsync(cancellationToken);

            _db.ContractTemplates.Add(template);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return template;
    }

    public async Task<ContractTemplate?> UpdateTemplateAsync(Guid id, string name, string htmlTemplate, bool isActive, bool isDefault, Guid? actingUserId = null, CancellationToken cancellationToken = default)
    {
        var template = await _db.ContractTemplates.FindAsync(new object[] { id }, cancellationToken);
        if (template is null)
            return null;

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(htmlTemplate))
            throw new ArgumentException("Template HTML is required.", nameof(htmlTemplate));

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (isDefault && !template.IsDefault)
                await ClearDefaultFlagAsync(cancellationToken);

            template.Name = name.Trim();
            template.HtmlTemplate = htmlTemplate.Trim();
            template.IsActive = isActive;
            template.IsDefault = isDefault;
            template.UpdatedBy = actingUserId;
            template.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return template;
    }

    public async Task<bool> SetDefaultTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.ContractTemplates.FindAsync(new object[] { id }, cancellationToken);
        if (template is null || !template.IsActive)
            return false;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await ClearDefaultFlagAsync(cancellationToken);
            template.IsDefault = true;
            template.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return true;
    }

    private async Task ClearDefaultFlagAsync(CancellationToken cancellationToken)
    {
        var existingDefaults = await _db.ContractTemplates
            .Where(t => t.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingDefaults)
        {
            existing.IsDefault = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }
}

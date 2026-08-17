using Microsoft.EntityFrameworkCore;
using NonCash.Core.Configuration;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Welcome-grant policy template management + resolution.
/// Resolution: active business assignment → default template → <c>CreditConfig</c> fallback.
/// </summary>
public class WelcomePolicyService : IWelcomePolicyService
{
    private readonly ApplicationDbContext _db;
    private readonly CreditConfig _config;

    public WelcomePolicyService(ApplicationDbContext db, CreditConfig config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ResolvedWelcomePolicy> ResolveForBusinessAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var policy = await _db.WelcomeGrantPolicies
            .AsNoTracking()
            .Where(p => p.BusinessId == businessId
                && p.IsActive
                && p.EffectiveFrom <= now
                && (p.EffectiveTo == null || p.EffectiveTo > now))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy is not null)
        {
            return new ResolvedWelcomePolicy(
                PolicyId: policy.Id,
                Name: policy.Name,
                WelcomeCredits: policy.WelcomeCredits,
                WelcomeCreditExpiryMonths: policy.WelcomeCreditExpiryMonths);
        }

        var defaultTemplate = await GetDefaultTemplateAsync(cancellationToken);
        if (defaultTemplate is not null)
        {
            return new ResolvedWelcomePolicy(
                PolicyId: null,
                Name: $"{defaultTemplate.Name} (default template)",
                WelcomeCredits: defaultTemplate.WelcomeCredits,
                WelcomeCreditExpiryMonths: defaultTemplate.WelcomeCreditExpiryMonths);
        }

        return new ResolvedWelcomePolicy(
            PolicyId: null,
            Name: "Default (config fallback)",
            WelcomeCredits: _config.WelcomeCredits,
            WelcomeCreditExpiryMonths: _config.WelcomeCreditExpiryMonths);
    }

    public async Task<WelcomeGrantPolicy> AssignTemplateToBusinessAsync(
        Guid businessId,
        Guid? templateId,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty)
            throw new InvalidOperationException("BusinessId is required.");

        WelcomeGrantPolicyTemplate template;
        if (templateId.HasValue)
        {
            template = await _db.WelcomeGrantPolicyTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId && t.IsActive, cancellationToken)
                ?? throw new InvalidOperationException($"Welcome policy template {templateId} not found or inactive.");
        }
        else
        {
            template = await GetDefaultTemplateAsync(cancellationToken)
                ?? throw new InvalidOperationException("No default welcome policy template is configured.");
        }

        var now = DateTime.UtcNow;

        // Deactivate any currently active assignment for this business so the new one is the only active assignment.
        var activeAssignments = await _db.WelcomeGrantPolicies
            .Where(p => p.BusinessId == businessId
                && p.IsActive
                && p.EffectiveFrom <= now
                && (p.EffectiveTo == null || p.EffectiveTo > now))
            .ToListAsync(cancellationToken);

        foreach (var existing in activeAssignments)
        {
            existing.IsActive = false;
            existing.EffectiveTo = now;
            existing.UpdatedBy = actingUserId;
        }

        var assignment = new WelcomeGrantPolicy
        {
            Name = template.Name,
            BusinessId = businessId,
            WelcomeGrantPolicyTemplateId = template.Id,
            WelcomeCredits = template.WelcomeCredits,
            WelcomeCreditExpiryMonths = template.WelcomeCreditExpiryMonths,
            EffectiveFrom = now,
            EffectiveTo = null,
            IsActive = true,
            CreatedBy = actingUserId
        };

        _db.WelcomeGrantPolicies.Add(assignment);
        await _db.SaveChangesAsync(cancellationToken);
        return assignment;
    }

    // ----- Template CRUD -----

    public async Task<IReadOnlyList<WelcomeGrantPolicyTemplate>> GetTemplatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.WelcomeGrantPolicyTemplates.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query.OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name).ToListAsync(cancellationToken);
    }

    public Task<WelcomeGrantPolicyTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.WelcomeGrantPolicyTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<WelcomeGrantPolicyTemplate?> GetDefaultTemplateAsync(CancellationToken cancellationToken = default)
        => _db.WelcomeGrantPolicyTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsDefault && t.IsActive, cancellationToken);

    public async Task<WelcomeGrantPolicyTemplate> CreateTemplateAsync(WelcomeGrantPolicyTemplate template, CancellationToken cancellationToken = default)
    {
        ValidateTemplate(template);

        if (template.IsDefault)
        {
            await UnsetExistingDefaultAsync(cancellationToken);
        }

        _db.WelcomeGrantPolicyTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task<WelcomeGrantPolicyTemplate> UpdateTemplateAsync(Guid id, WelcomeGrantPolicyTemplate changes, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WelcomeGrantPolicyTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Welcome policy template {id} not found.");

        ValidateTemplate(changes);

        if (changes.IsDefault && !existing.IsDefault)
        {
            await UnsetExistingDefaultAsync(cancellationToken);
        }

        existing.Name = changes.Name;
        existing.WelcomeCredits = changes.WelcomeCredits;
        existing.WelcomeCreditExpiryMonths = changes.WelcomeCreditExpiryMonths;
        existing.IsActive = changes.IsActive;
        existing.IsDefault = changes.IsDefault;
        existing.UpdatedBy = changes.UpdatedBy;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeactivateTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WelcomeGrantPolicyTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Welcome policy template {id} not found.");

        existing.IsActive = false;
        existing.IsDefault = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDefaultTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WelcomeGrantPolicyTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Welcome policy template {id} not found.");

        await UnsetExistingDefaultAsync(cancellationToken);

        existing.IsDefault = true;
        existing.IsActive = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ----- Assignment CRUD -----

    public async Task<IReadOnlyList<WelcomeGrantPolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.WelcomeGrantPolicies
            .AsNoTracking()
            .Include(p => p.Business)
            .Include(p => p.WelcomeGrantPolicyTemplate)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query.OrderByDescending(p => p.EffectiveFrom).ToListAsync(cancellationToken);
    }

    public Task<WelcomeGrantPolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.WelcomeGrantPolicies
            .AsNoTracking()
            .Include(p => p.Business)
            .Include(p => p.WelcomeGrantPolicyTemplate)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<WelcomeGrantPolicy> CreatePolicyAsync(WelcomeGrantPolicy policy, CancellationToken cancellationToken = default)
    {
        ValidateAssignment(policy);

        _db.WelcomeGrantPolicies.Add(policy);
        await _db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task<WelcomeGrantPolicy> UpdatePolicyAsync(Guid id, WelcomeGrantPolicy changes, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WelcomeGrantPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Welcome policy assignment {id} not found.");

        ValidateAssignment(changes);

        existing.Name = changes.Name;
        existing.BusinessId = changes.BusinessId;
        existing.WelcomeGrantPolicyTemplateId = changes.WelcomeGrantPolicyTemplateId;
        existing.WelcomeCredits = changes.WelcomeCredits;
        existing.WelcomeCreditExpiryMonths = changes.WelcomeCreditExpiryMonths;
        existing.EffectiveFrom = changes.EffectiveFrom;
        existing.EffectiveTo = changes.EffectiveTo;
        existing.IsActive = changes.IsActive;
        existing.UpdatedBy = changes.UpdatedBy;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeactivatePolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WelcomeGrantPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Welcome policy assignment {id} not found.");

        existing.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UnsetExistingDefaultAsync(CancellationToken cancellationToken)
    {
        var existingDefault = await _db.WelcomeGrantPolicyTemplates
            .FirstOrDefaultAsync(t => t.IsDefault, cancellationToken);

        if (existingDefault is not null)
        {
            existingDefault.IsDefault = false;
        }
    }

    private static void ValidateTemplate(WelcomeGrantPolicyTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
            throw new InvalidOperationException("Template name is required.");
        if (template.WelcomeCredits < 0)
            throw new InvalidOperationException("Welcome credits cannot be negative.");
    }

    private static void ValidateAssignment(WelcomeGrantPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Name))
            throw new InvalidOperationException("Policy name is required.");
        if (policy.BusinessId == Guid.Empty)
            throw new InvalidOperationException("BusinessId is required.");
        if (policy.WelcomeCredits < 0)
            throw new InvalidOperationException("Welcome credits cannot be negative.");
        if (policy.EffectiveTo != null && policy.EffectiveTo <= policy.EffectiveFrom)
            throw new InvalidOperationException("EffectiveTo must be after EffectiveFrom.");
    }
}

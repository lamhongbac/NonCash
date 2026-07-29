using Microsoft.EntityFrameworkCore;
using NonCash.Core.Configuration;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Credit pricing policy management + resolution (Epic 10).
/// Resolution: Brand-scoped → BrandGroup-scoped → Global (most recent EffectiveFrom wins
/// within a scope) → CreditConfig fallback when no DB policy matches.
/// </summary>
public class CreditPolicyService : ICreditPolicyService
{
    private readonly ApplicationDbContext _db;
    private readonly CreditConfig _config;

    public CreditPolicyService(ApplicationDbContext db, CreditConfig config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ResolvedCreditPolicy> ResolveForBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var groupIds = await _db.BrandGroupMembers
            .Where(m => m.BrandId == brandId)
            .Select(m => m.BrandGroupId)
            .ToListAsync(cancellationToken);

        var policy = await _db.CreditPricingPolicies
            .AsNoTracking()
            .Where(p => p.IsActive && p.EffectiveFrom <= now && (p.EffectiveTo == null || p.EffectiveTo > now))
            .Where(p => (p.Scope == PolicyScope.Brand && p.BrandId == brandId)
                     || (p.Scope == PolicyScope.BrandGroup && p.BrandGroupId != null && groupIds.Contains(p.BrandGroupId.Value))
                     || p.Scope == PolicyScope.Global)
            .OrderByDescending(p => p.Scope)          // Brand(2) > BrandGroup(1) > Global(0)
            .ThenByDescending(p => p.EffectiveFrom)   // newest version within a scope
            .FirstOrDefaultAsync(cancellationToken);

        if (policy is null)
        {
            return new ResolvedCreditPolicy(
                PolicyId: null,
                Name: "Default (config fallback)",
                Scope: null,
                PricePerCreditVnd: _config.PricePerCreditVnd,
                CreditExpiryMonths: _config.CreditExpiryMonths,
                WelcomeCredits: _config.WelcomeCredits,
                WelcomeCreditExpiryMonths: _config.WelcomeCreditExpiryMonths,
                LowBalanceWarningPct: _config.LowBalanceWarningPercent,
                ExpiryWarningDays: _config.ExpiryWarningDays,
                AdjustmentApprovalThreshold: _config.AdjustmentApprovalThreshold);
        }

        return new ResolvedCreditPolicy(
            PolicyId: policy.Id,
            Name: policy.Name,
            Scope: policy.Scope,
            PricePerCreditVnd: policy.PricePerCreditVnd,
            CreditExpiryMonths: policy.CreditExpiryMonths,
            WelcomeCredits: policy.WelcomeCredits,
            WelcomeCreditExpiryMonths: policy.WelcomeCreditExpiryMonths,
            LowBalanceWarningPct: policy.LowBalanceWarningPct,
            ExpiryWarningDays: policy.ExpiryWarningDays,
            AdjustmentApprovalThreshold: policy.AdjustmentApprovalThreshold);
    }

    // ----- Policy CRUD -----

    public async Task<IReadOnlyList<CreditPricingPolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.CreditPricingPolicies
            .AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.BrandGroup)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public Task<CreditPricingPolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.CreditPricingPolicies
            .AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.BrandGroup)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<CreditPricingPolicy> CreatePolicyAsync(CreditPricingPolicy policy, CancellationToken cancellationToken = default)
    {
        ValidatePolicy(policy);

        _db.CreditPricingPolicies.Add(policy);
        await _db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task<CreditPricingPolicy> UpdatePolicyAsync(Guid id, CreditPricingPolicy changes, CancellationToken cancellationToken = default)
    {
        var existing = await _db.CreditPricingPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Pricing policy {id} not found.");

        ValidatePolicy(changes);

        existing.Name = changes.Name;
        existing.Scope = changes.Scope;
        existing.BrandGroupId = changes.BrandGroupId;
        existing.BrandId = changes.BrandId;
        existing.PricePerCreditVnd = changes.PricePerCreditVnd;
        existing.CreditExpiryMonths = changes.CreditExpiryMonths;
        existing.WelcomeCredits = changes.WelcomeCredits;
        existing.WelcomeCreditExpiryMonths = changes.WelcomeCreditExpiryMonths;
        existing.LowBalanceWarningPct = changes.LowBalanceWarningPct;
        existing.ExpiryWarningDays = changes.ExpiryWarningDays;
        existing.AdjustmentApprovalThreshold = changes.AdjustmentApprovalThreshold;
        existing.EffectiveFrom = changes.EffectiveFrom;
        existing.EffectiveTo = changes.EffectiveTo;
        existing.IsActive = changes.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeactivatePolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.CreditPricingPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Pricing policy {id} not found.");

        existing.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePolicy(CreditPricingPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Name))
            throw new InvalidOperationException("Policy name is required.");
        if (policy.PricePerCreditVnd < 0)
            throw new InvalidOperationException("Price per credit cannot be negative.");
        if (policy.WelcomeCredits < 0)
            throw new InvalidOperationException("Welcome credits cannot be negative.");
        if (policy.Scope == PolicyScope.BrandGroup && policy.BrandGroupId is null)
            throw new InvalidOperationException("BrandGroupId is required for a BrandGroup-scoped policy.");
        if (policy.Scope == PolicyScope.Brand && policy.BrandId is null)
            throw new InvalidOperationException("BrandId is required for a Brand-scoped policy.");
        if (policy.EffectiveTo != null && policy.EffectiveTo <= policy.EffectiveFrom)
            throw new InvalidOperationException("EffectiveTo must be after EffectiveFrom.");
    }

    // ----- Brand groups -----

    public async Task<IReadOnlyList<BrandGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
        => await _db.BrandGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .ThenInclude(m => m.Brand)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

    public Task<BrandGroup?> GetGroupAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.BrandGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .ThenInclude(m => m.Brand)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<BrandGroup> CreateGroupAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Group name is required.");

        var group = new BrandGroup { Name = name.Trim(), Description = description };
        _db.BrandGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);
        return group;
    }

    public async Task<BrandGroup> UpdateGroupAsync(Guid id, string name, string? description, bool isActive, CancellationToken cancellationToken = default)
    {
        var group = await _db.BrandGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Brand group {id} not found.");

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Group name is required.");

        group.Name = name.Trim();
        group.Description = description;
        group.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
        return group;
    }

    public async Task SetGroupMembersAsync(Guid groupId, IReadOnlyCollection<Guid> brandIds, CancellationToken cancellationToken = default)
    {
        var group = await _db.BrandGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException($"Brand group {groupId} not found.");

        var desired = brandIds.Distinct().ToHashSet();

        var toRemove = group.Members.Where(m => !desired.Contains(m.BrandId)).ToList();
        foreach (var member in toRemove)
        {
            _db.BrandGroupMembers.Remove(member);
        }

        var current = group.Members.Select(m => m.BrandId).ToHashSet();
        foreach (var brandId in desired.Where(id => !current.Contains(id)))
        {
            _db.BrandGroupMembers.Add(new BrandGroupMember { BrandGroupId = groupId, BrandId = brandId });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

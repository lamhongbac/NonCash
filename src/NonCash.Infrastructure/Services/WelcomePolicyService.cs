using Microsoft.EntityFrameworkCore;
using NonCash.Core.Configuration;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Welcome-grant policy management + resolution (Epic 10 refactor).
/// Resolution: most recent active, in-effect policy for the business →
/// <c>CreditConfig</c> fallback when no DB policy matches.
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
            .OrderByDescending(p => p.EffectiveFrom)   // newest version wins
            .FirstOrDefaultAsync(cancellationToken);

        if (policy is null)
        {
            return new ResolvedWelcomePolicy(
                PolicyId: null,
                Name: "Default (config fallback)",
                WelcomeCredits: _config.WelcomeCredits,
                WelcomeCreditExpiryMonths: _config.WelcomeCreditExpiryMonths);
        }

        return new ResolvedWelcomePolicy(
            PolicyId: policy.Id,
            Name: policy.Name,
            WelcomeCredits: policy.WelcomeCredits,
            WelcomeCreditExpiryMonths: policy.WelcomeCreditExpiryMonths);
    }

    // ----- Policy CRUD -----

    public async Task<IReadOnlyList<WelcomeGrantPolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.WelcomeGrantPolicies
            .AsNoTracking()
            .Include(p => p.Business)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public Task<WelcomeGrantPolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.WelcomeGrantPolicies
            .AsNoTracking()
            .Include(p => p.Business)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<WelcomeGrantPolicy> CreatePolicyAsync(WelcomeGrantPolicy policy, CancellationToken cancellationToken = default)
    {
        ValidatePolicy(policy);

        _db.WelcomeGrantPolicies.Add(policy);
        await _db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task<WelcomeGrantPolicy> UpdatePolicyAsync(Guid id, WelcomeGrantPolicy changes, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WelcomeGrantPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Welcome policy {id} not found.");

        ValidatePolicy(changes);

        existing.Name = changes.Name;
        existing.BusinessId = changes.BusinessId;
        existing.WelcomeCredits = changes.WelcomeCredits;
        existing.WelcomeCreditExpiryMonths = changes.WelcomeCreditExpiryMonths;
        existing.EffectiveFrom = changes.EffectiveFrom;
        existing.EffectiveTo = changes.EffectiveTo;
        existing.IsActive = changes.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeactivatePolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WelcomeGrantPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Welcome policy {id} not found.");

        existing.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePolicy(WelcomeGrantPolicy policy)
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

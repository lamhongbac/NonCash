using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

public class SubscriptionFeePolicyService : ISubscriptionFeePolicyService
{
    private readonly ApplicationDbContext _db;

    public SubscriptionFeePolicyService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SubscriptionFeePolicy?> GetEffectivePolicyAsync(DateTime? asOf = null, CancellationToken cancellationToken = default)
    {
        var now = asOf ?? DateTime.UtcNow;

        return await _db.SubscriptionFeePolicies
            .AsNoTracking()
            .Where(p => p.IsActive
                && p.EffectiveFrom <= now
                && (p.EffectiveTo == null || p.EffectiveTo >= now))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionFeePolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.SubscriptionFeePolicies.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        return await query
            .OrderByDescending(p => p.EffectiveFrom)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionFeePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.SubscriptionFeePolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<SubscriptionFeePolicy> CreatePolicyAsync(SubscriptionFeePolicy policy, Guid? actingUserId = null, CancellationToken cancellationToken = default)
    {
        ValidatePolicy(policy);

        if (policy.IsActive)
            await ValidateNoOverlapAsync(policy, excludeId: null, cancellationToken);

        var now = DateTime.UtcNow;
        policy.Id = Guid.NewGuid();
        policy.CreatedBy = actingUserId;
        policy.CreatedAt = now;
        policy.UpdatedAt = now;

        _db.SubscriptionFeePolicies.Add(policy);
        await _db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task<SubscriptionFeePolicy?> UpdatePolicyAsync(Guid id, SubscriptionFeePolicy policy, Guid? actingUserId = null, CancellationToken cancellationToken = default)
    {
        var existing = await _db.SubscriptionFeePolicies.FindAsync(new object[] { id }, cancellationToken);
        if (existing is null)
            return null;

        ValidatePolicy(policy);

        if (policy.IsActive)
            await ValidateNoOverlapAsync(policy, id, cancellationToken);

        existing.Name = policy.Name.Trim();
        existing.AmountVnd = policy.AmountVnd;
        existing.IsFree = policy.IsFree;
        existing.MinimumCommitmentMonths = policy.MinimumCommitmentMonths;
        existing.EffectiveFrom = policy.EffectiveFrom;
        existing.EffectiveTo = policy.EffectiveTo;
        existing.IsActive = policy.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task ValidateNoOverlapAsync(SubscriptionFeePolicy policy, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var overlapping = await _db.SubscriptionFeePolicies
            .AsNoTracking()
            .Where(p => p.IsActive
                && p.Id != excludeId
                && p.EffectiveFrom <= (policy.EffectiveTo ?? DateTime.MaxValue)
                && (p.EffectiveTo == null || p.EffectiveTo >= policy.EffectiveFrom))
            .FirstOrDefaultAsync(cancellationToken);

        if (overlapping is not null)
        {
            var range = overlapping.EffectiveTo.HasValue
                ? $"{overlapping.EffectiveFrom:yyyy-MM-dd} to {overlapping.EffectiveTo.Value:yyyy-MM-dd}"
                : $"{overlapping.EffectiveFrom:yyyy-MM-dd} onwards";
            throw new ArgumentException(
                $"The new policy date range overlaps with existing active policy '{overlapping.Name}' ({range}). " +
                "Please adjust the dates so that active policies do not overlap, or deactivate the existing policy first.");
        }
    }

    private static void ValidatePolicy(SubscriptionFeePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Name))
            throw new ArgumentException("Policy name is required.", nameof(policy));

        if (policy.EffectiveTo.HasValue && policy.EffectiveTo.Value < policy.EffectiveFrom)
            throw new ArgumentException("EffectiveTo must be on or after EffectiveFrom.", nameof(policy));

        if (policy.MinimumCommitmentMonths < 1)
            throw new ArgumentException("Minimum commitment months must be at least 1.", nameof(policy));

        if (!policy.IsFree && policy.AmountVnd <= 0)
            throw new ArgumentException("Amount must be greater than 0 when the policy is not free.", nameof(policy));
    }
}

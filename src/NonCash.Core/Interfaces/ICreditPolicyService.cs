using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Credit pricing policy management and resolution (Epic 10).
/// Resolution priority: Brand-scoped → BrandGroup-scoped → Global → CreditConfig fallback.
/// </summary>
public interface ICreditPolicyService
{
    /// <summary>Resolves the effective policy for a brand at the current time.</summary>
    Task<ResolvedCreditPolicy> ResolveForBrandAsync(Guid brandId, CancellationToken cancellationToken = default);

    // ----- Policy CRUD (Admin) -----
    Task<IReadOnlyList<CreditPricingPolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CreditPricingPolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CreditPricingPolicy> CreatePolicyAsync(CreditPricingPolicy policy, CancellationToken cancellationToken = default);
    Task<CreditPricingPolicy> UpdatePolicyAsync(Guid id, CreditPricingPolicy changes, CancellationToken cancellationToken = default);
    Task DeactivatePolicyAsync(Guid id, CancellationToken cancellationToken = default);

    // ----- Brand groups (Admin) -----
    Task<IReadOnlyList<BrandGroup>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<BrandGroup?> GetGroupAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BrandGroup> CreateGroupAsync(string name, string? description, CancellationToken cancellationToken = default);
    Task<BrandGroup> UpdateGroupAsync(Guid id, string name, string? description, bool isActive, CancellationToken cancellationToken = default);
    /// <summary>Replaces the group's member list with the given brand ids.</summary>
    Task SetGroupMembersAsync(Guid groupId, IReadOnlyCollection<Guid> brandIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// The policy values in force for a brand, after Brand → Group → Global → config-fallback resolution.
/// PolicyId is null when no DB policy matched (CreditConfig fallback).
/// </summary>
public record ResolvedCreditPolicy(
    Guid? PolicyId,
    string Name,
    PolicyScope? Scope,
    decimal PricePerCreditVnd,
    int? CreditExpiryMonths,
    int? LowBalanceWarningPct,
    int? ExpiryWarningDays,
    int? AdjustmentApprovalThreshold);

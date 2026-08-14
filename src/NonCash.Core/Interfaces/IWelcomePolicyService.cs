using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Welcome-grant policy management and resolution (Epic 10 refactor).
/// Welcome is a per-business commercial term: every new brand a business launches
/// receives <see cref="WelcomeGrantPolicy.WelcomeCredits"/> on activation, resolved from
/// the business's most recent active policy and falling back to <c>CreditConfig</c>
/// defaults when no policy is set.
/// </summary>
public interface IWelcomePolicyService
{
    /// <summary>
    /// Resolves the effective welcome policy for a business
    /// (Business policy → <c>CreditConfig</c> fallback).
    /// </summary>
    Task<ResolvedWelcomePolicy> ResolveForBusinessAsync(Guid businessId, CancellationToken cancellationToken = default);

    // ----- Policy CRUD (Admin) -----
    Task<IReadOnlyList<WelcomeGrantPolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicy> CreatePolicyAsync(WelcomeGrantPolicy policy, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicy> UpdatePolicyAsync(Guid id, WelcomeGrantPolicy changes, CancellationToken cancellationToken = default);
    Task DeactivatePolicyAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// The welcome policy values in force for a business, after Business policy →
/// <c>CreditConfig</c> fallback resolution. PolicyId is null when no DB policy matched
/// (config fallback).
/// </summary>
public record ResolvedWelcomePolicy(
    Guid? PolicyId,
    string Name,
    int WelcomeCredits,
    int? WelcomeCreditExpiryMonths);

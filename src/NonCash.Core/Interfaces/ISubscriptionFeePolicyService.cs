using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Date-ranged subscription fee policy management and resolution.
/// </summary>
public interface ISubscriptionFeePolicyService
{
    /// <summary>
    /// Returns the active policy effective at <paramref name="asOf"/> (UTC). If no policy is in force,
    /// returns null so the caller can fall back to configuration defaults.
    /// </summary>
    Task<SubscriptionFeePolicy?> GetEffectivePolicyAsync(DateTime? asOf = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionFeePolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<SubscriptionFeePolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SubscriptionFeePolicy> CreatePolicyAsync(SubscriptionFeePolicy policy, Guid? actingUserId = null, CancellationToken cancellationToken = default);
    Task<SubscriptionFeePolicy?> UpdatePolicyAsync(Guid id, SubscriptionFeePolicy policy, Guid? actingUserId = null, CancellationToken cancellationToken = default);
}

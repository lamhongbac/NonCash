using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IBrandCustomerRepository
{
    /// <summary>
    /// Idempotently ensures a brand-customer mapping exists: inserts when absent,
    /// no-op when already present (Source is never upgraded). Saves changes.
    /// </summary>
    Task EnsureAsync(Guid brandId, Guid customerId, BrandCustomerSource source, Guid? createdBy = null, CancellationToken cancellationToken = default);

    Task<BrandCustomer?> FindAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Returns the existing mappings for the given customers (unknown ids are simply absent).</summary>
    Task<IReadOnlyList<BrandCustomer>> GetForBrandAsync(Guid brandId, IEnumerable<Guid> customerIds, CancellationToken cancellationToken = default);

    Task<bool> IsBlockedAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or clears the per-brand block. Throws KeyNotFoundException when no mapping exists.
    /// </summary>
    Task SetBlockedAsync(Guid brandId, Guid customerId, bool blocked, CancellationToken cancellationToken = default);
}

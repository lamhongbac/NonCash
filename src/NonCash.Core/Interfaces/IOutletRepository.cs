using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IOutletRepository : IRepository<Outlet>
{
    Task<IEnumerable<Outlet>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<int> CountByBrandAsync(Guid brandId, CancellationToken cancellationToken = default);
    /// <summary>Finds the active outlet with the given code within the brand (case-insensitive). Returns null if not found.</summary>
    Task<Outlet?> GetByCodeAsync(Guid brandId, string code, CancellationToken cancellationToken = default);
}

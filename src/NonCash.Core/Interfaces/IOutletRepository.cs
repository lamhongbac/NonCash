using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IOutletRepository : IRepository<Outlet>
{
    Task<IEnumerable<Outlet>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<int> CountByBrandAsync(Guid brandId, CancellationToken cancellationToken = default);
}

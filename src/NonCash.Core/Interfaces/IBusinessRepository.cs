using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IBusinessRepository : IRepository<Business>
{
    Task<Business?> GetByTaxCodeAsync(string taxCode, CancellationToken cancellationToken = default);
    Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken cancellationToken = default);
}

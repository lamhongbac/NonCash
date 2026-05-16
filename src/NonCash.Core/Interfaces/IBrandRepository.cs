using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IBrandRepository : IRepository<Brand>
{
    Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken cancellationToken = default);
    Task<Brand?> GetByTaxCodeAsync(string taxCode, CancellationToken cancellationToken = default);
}

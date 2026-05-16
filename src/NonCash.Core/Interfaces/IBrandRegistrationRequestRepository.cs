using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IBrandRegistrationRequestRepository : IRepository<BrandRegistrationRequest>
{
    Task<BrandRegistrationRequest?> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default);
}

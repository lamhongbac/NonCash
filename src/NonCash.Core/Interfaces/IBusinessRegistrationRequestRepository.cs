using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IBusinessRegistrationRequestRepository : IRepository<BusinessRegistrationRequest>
{
    Task<BusinessRegistrationRequest?> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default);
}

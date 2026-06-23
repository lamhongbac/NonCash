using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IUserAccountRepository : IRepository<UserAccount>
{
    Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserAccount>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}

using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IMemberAccountRepository : IRepository<MemberAccount>
{
    Task<MemberAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<MemberAccount?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}

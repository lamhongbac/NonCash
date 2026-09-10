using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IMemberAccountRepository : IRepository<MemberAccount>
{
    Task<MemberAccount?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}

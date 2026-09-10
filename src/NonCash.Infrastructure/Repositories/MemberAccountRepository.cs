using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class MemberAccountRepository : Repository<MemberAccount>, IMemberAccountRepository
{
    private readonly ApplicationDbContext _context;

    public MemberAccountRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<MemberAccount?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.MemberAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CustomerId == customerId, cancellationToken);
    }
}

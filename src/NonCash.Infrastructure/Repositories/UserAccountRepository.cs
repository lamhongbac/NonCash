using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class UserAccountRepository : Repository<UserAccount>, IUserAccountRepository
{
    private readonly ApplicationDbContext _context;

    public UserAccountRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .AnyAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<IEnumerable<UserAccount>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .AsNoTracking()
            .Where(u => u.BrandId == brandId)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserAccount?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CustomerId == customerId, cancellationToken);
    }
}

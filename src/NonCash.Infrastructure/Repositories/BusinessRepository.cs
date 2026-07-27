using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class BusinessRepository : Repository<Business>, IBusinessRepository
{
    private readonly ApplicationDbContext _context;

    public BusinessRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken cancellationToken = default)
    {
        return await _context.Businesses.AnyAsync(b => b.TaxCode == taxCode, cancellationToken);
    }

    public async Task<Business?> GetByTaxCodeAsync(string taxCode, CancellationToken cancellationToken = default)
    {
        return await _context.Businesses.AsNoTracking().FirstOrDefaultAsync(b => b.TaxCode == taxCode, cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class BrandRepository : Repository<Brand>, IBrandRepository
{
    private readonly ApplicationDbContext _context;

    public BrandRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken cancellationToken = default)
    {
        return await _context.Brands.AnyAsync(b => b.TaxCode == taxCode, cancellationToken);
    }

    public async Task<Brand?> GetByTaxCodeAsync(string taxCode, CancellationToken cancellationToken = default)
    {
        return await _context.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.TaxCode == taxCode, cancellationToken);
    }
}

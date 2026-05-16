using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class OutletRepository : Repository<Outlet>, IOutletRepository
{
    private readonly ApplicationDbContext _context;

    public OutletRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Outlet>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _context.Outlets
            .AsNoTracking()
            .Where(o => o.BrandId == brandId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _context.Outlets
            .CountAsync(o => o.BrandId == brandId, cancellationToken);
    }
}

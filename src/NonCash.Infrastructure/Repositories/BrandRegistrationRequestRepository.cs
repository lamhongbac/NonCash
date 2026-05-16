using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class BrandRegistrationRequestRepository : Repository<BrandRegistrationRequest>, IBrandRegistrationRequestRepository
{
    private readonly ApplicationDbContext _context;

    public BrandRegistrationRequestRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<BrandRegistrationRequest?> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _context.BrandRegistrationRequests
            .FirstOrDefaultAsync(r => r.BrandId == brandId, cancellationToken);
    }
}

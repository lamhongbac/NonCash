using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class BusinessRegistrationRequestRepository : Repository<BusinessRegistrationRequest>, IBusinessRegistrationRequestRepository
{
    private readonly ApplicationDbContext _context;

    public BusinessRegistrationRequestRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<BusinessRegistrationRequest?> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _context.BusinessRegistrationRequests
            .FirstOrDefaultAsync(r => r.BrandId == brandId, cancellationToken);
    }
}

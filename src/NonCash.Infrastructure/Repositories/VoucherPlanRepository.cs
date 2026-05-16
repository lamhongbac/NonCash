using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class VoucherPlanRepository : Repository<VoucherPlanHeader>, IVoucherPlanRepository
{
    private readonly ApplicationDbContext _context;

    public VoucherPlanRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<VoucherPlanHeader>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _context.VoucherPlanHeaders
            .Include(p => p.PlanOutlets)
            .Where(p => p.BrandId == brandId)
            .OrderByDescending(p => p.PlanDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VoucherPlanHeader>> ListByBrandAndStatusAsync(Guid brandId, ApprovalStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.VoucherPlanHeaders
            .Include(p => p.PlanOutlets)
            .Where(p => p.BrandId == brandId && p.ApprovalStatus == status)
            .OrderByDescending(p => p.PlanDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<VoucherPlanHeader?> GetByIdWithOutletsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.VoucherPlanHeaders
            .Include(p => p.PlanOutlets)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class BrandCustomerRepository : IBrandCustomerRepository
{
    private readonly ApplicationDbContext _context;

    public BrandCustomerRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EnsureAsync(Guid brandId, Guid customerId, BrandCustomerSource source, Guid? createdBy = null, CancellationToken cancellationToken = default)
    {
        var exists = await _context.BrandCustomers
            .AsNoTracking()
            .AnyAsync(bc => bc.BrandId == brandId && bc.CustomerId == customerId, cancellationToken);

        if (exists)
            return; // Idempotent: never upgrade Source or overwrite CreatedBy on re-link.

        _context.BrandCustomers.Add(new BrandCustomer
        {
            BrandId = brandId,
            CustomerId = customerId,
            Source = source,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<BrandCustomer?> FindAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.BrandCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(bc => bc.BrandId == brandId && bc.CustomerId == customerId, cancellationToken);
    }

    public async Task<IReadOnlyList<BrandCustomer>> GetForBrandAsync(Guid brandId, IEnumerable<Guid> customerIds, CancellationToken cancellationToken = default)
    {
        var ids = customerIds.ToList();
        if (ids.Count == 0)
            return Array.Empty<BrandCustomer>();

        return await _context.BrandCustomers
            .AsNoTracking()
            .Where(bc => bc.BrandId == brandId && ids.Contains(bc.CustomerId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsBlockedAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.BrandCustomers
            .AsNoTracking()
            .AnyAsync(bc => bc.BrandId == brandId && bc.CustomerId == customerId && bc.IsBlocked, cancellationToken);
    }

    public async Task SetBlockedAsync(Guid brandId, Guid customerId, bool blocked, CancellationToken cancellationToken = default)
    {
        var mapping = await _context.BrandCustomers
            .FirstOrDefaultAsync(bc => bc.BrandId == brandId && bc.CustomerId == customerId, cancellationToken);

        if (mapping == null)
            throw new KeyNotFoundException($"No customer mapping found for brand '{brandId}' and customer '{customerId}'.");

        if (mapping.IsBlocked == blocked)
            return; // Already in the requested state.

        mapping.IsBlocked = blocked;
        mapping.BlockedAt = blocked ? DateTime.UtcNow : null;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}

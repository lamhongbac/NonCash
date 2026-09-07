using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class UserOutletRepository : Repository<UserOutlet>, IUserOutletRepository
{
    private readonly ApplicationDbContext _context;

    public UserOutletRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Outlet>> GetOutletsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserOutlet>()
            .AsNoTracking()
            .Where(uo => uo.UserId == userId)
            .Select(uo => uo.Outlet!)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserAccount>> GetUsersForOutletAsync(Guid outletId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserOutlet>()
            .AsNoTracking()
            .Where(uo => uo.OutletId == outletId)
            .Select(uo => uo.User!)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceAssignmentsAsync(Guid userId, Guid brandId, IEnumerable<Guid> outletIds, CancellationToken cancellationToken = default)
    {
        // Load existing assignments for this user whose outlet belongs to the given brand.
        var existing = await _context.Set<UserOutlet>()
            .Include(uo => uo.Outlet)
            .Where(uo => uo.UserId == userId && uo.Outlet!.BrandId == brandId)
            .ToListAsync(cancellationToken);

        foreach (var assignment in existing)
            _context.Set<UserOutlet>().Remove(assignment);

        foreach (var outletId in outletIds.Distinct())
        {
            _context.Set<UserOutlet>().Add(new UserOutlet
            {
                UserId = userId,
                OutletId = outletId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

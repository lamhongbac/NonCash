using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IUserOutletRepository : IRepository<UserOutlet>
{
    /// <summary>All outlets the given staff user is assigned to (for staff-login session scoping).</summary>
    Task<IEnumerable<Outlet>> GetOutletsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>All StoreStaff users assigned to the given outlet.</summary>
    Task<IEnumerable<UserAccount>> GetUsersForOutletAsync(Guid outletId, CancellationToken cancellationToken = default);

    /// <summary>Replace the assignment set for a user within a brand: remove old, add new.</summary>
    Task ReplaceAssignmentsAsync(Guid userId, Guid brandId, IEnumerable<Guid> outletIds, CancellationToken cancellationToken = default);
}

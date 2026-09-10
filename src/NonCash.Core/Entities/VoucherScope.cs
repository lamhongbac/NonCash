namespace NonCash.Core.Entities;

/// <summary>
/// Epic 3: Hierarchical applicability scope of a voucher plan.
/// Each list holds ids at one org level (Business/company -> Brand -> Outlet).
/// An empty list at a level means "all under that level" — e.g. empty <see cref="Outlets"/>
/// with a brand set means every outlet of that brand; empty <see cref="Brands"/> with a
/// company set means the whole company. Persisted as a single jsonb column ("scope") on
/// voucher_plan_headers via an EF Core owned entity mapped with ToJson (1 plan = 1 row).
/// Referential integrity is enforced in application logic (not PK/FK): Guid ids of deleted
/// entities are inert (never reused), so a stale id in scope is harmless.
/// </summary>
public class VoucherScope
{
    public List<Guid> Companies { get; set; } = new();
    public List<Guid> Brands { get; set; } = new();
    public List<Guid> Outlets { get; set; } = new();

    /// <summary>
    /// Returns true if the given outlet is covered by this scope.
    /// Empty Outlets = all outlets under the owning brand (hierarchy-aware).
    /// Non-empty Outlets = only the explicitly listed outlets.
    /// The caller must separately verify that the outlet belongs to the plan's brand
    /// (same-brand gate); this method only checks the scope-level outlet list.
    /// </summary>
    public bool CoversOutlet(Guid outletId) =>
        Outlets.Count == 0 || Outlets.Contains(outletId);
}

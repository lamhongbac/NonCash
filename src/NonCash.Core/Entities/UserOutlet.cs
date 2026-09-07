namespace NonCash.Core.Entities;

/// <summary>
/// Join table that links a StoreStaff user to one or more outlets within a single brand
/// (CR-2026-09-07-18). A StoreStaff must be assigned to at least one outlet to log in.
/// Deleting the outlet (cascade) or the user (cascade) removes the assignment.
/// </summary>
public class UserOutlet : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid OutletId { get; set; }

    // Navigation properties
    public UserAccount? User { get; set; }
    public Outlet? Outlet { get; set; }
}

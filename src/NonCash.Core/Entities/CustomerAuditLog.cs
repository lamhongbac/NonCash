namespace NonCash.Core.Entities;

/// <summary>
/// CR-2026-09-07-14: audit trail for platform-admin edits to the shared global
/// customer record. Email is the voucher delivery channel, so every admin overwrite
/// records who changed which field, when (CreatedAt), and from which value to which.
/// Brand writes never produce entries here — they are fill-empty-only (CR-2026-09-06-11).
/// </summary>
public class CustomerAuditLog : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

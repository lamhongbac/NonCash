namespace NonCash.Core.Entities;

/// <summary>
/// Append-only prepaid credit ledger (Epic 9). Balance = SUM(Amount) per brand.
/// Billing rule: each voucher consumes exactly 1 credit, once in its lifetime,
/// at its value moment — Gift when sold, Complimentary when redeemed.
/// </summary>
public class CreditLedgerEntry : BaseEntity
{
    /// <summary>Brand whose credit balance this entry affects.</summary>
    public Guid BrandId { get; set; }

    /// <summary>Ledger entry type.</summary>
    public CreditEntryType EntryType { get; set; }

    /// <summary>Signed credit amount: positive for Grant/Purchase, negative for Consumption.</summary>
    public int Amount { get; set; }

    /// <summary>Free-text reference (bank transfer ref, note, or consumption context).</summary>
    public string? Reference { get; set; }

    /// <summary>
    /// The voucher charged by a Consumption entry. Unique when set —
    /// enforces the "1 voucher = max 1 credit" invariant.
    /// </summary>
    public Guid? VoucherDetailId { get; set; }

    /// <summary>User who created the entry (top-ups/adjustments); null for system consumption.</summary>
    public Guid? CreatedBy { get; set; }

    // Navigation
    public Brand? Brand { get; set; }
}

public enum CreditEntryType
{
    Grant = 0,
    Purchase = 1,
    Consumption = 2,
    Adjustment = 3
}

namespace NonCash.Core.Entities;

/// <summary>
/// One voucher's single credit charge, drawn FIFO from a batch (Epic 10).
/// VoucherDetailId is unique — enforces the "1 voucher = max 1 credit" invariant.
/// </summary>
public class CreditConsumption : BaseEntity
{
    public Guid BatchId { get; set; }

    public Guid BrandId { get; set; }

    /// <summary>The voucher charged. Unique across all consumptions.</summary>
    public Guid VoucherDetailId { get; set; }

    /// <summary>Consumption context (e.g. "gift-sold", "complimentary-redeemed").</summary>
    public string? Reference { get; set; }

    // Navigation
    public CreditBatch? Batch { get; set; }
    public Brand? Brand { get; set; }
}

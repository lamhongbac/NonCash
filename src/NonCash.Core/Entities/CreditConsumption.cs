namespace NonCash.Core.Entities;

/// <summary>
/// A credit charge ledger row (Epic 10). Two shapes:
///   • Per-voucher: <see cref="VoucherDetailId"/> set, <see cref="Quantity"/> = 1 (unique → "1 voucher = max 1 credit").
///   • Plan-level approval charge: <see cref="PlanId"/> set, <see cref="Quantity"/> = N approved vouchers (unique → one charge per plan).
/// <see cref="BatchId"/> is null on a plan-level aggregate row (the FIFO draw spans batches; balance lives on the batches).
/// </summary>
public class CreditConsumption : BaseEntity
{
    public Guid? BatchId { get; set; }

    public Guid BrandId { get; set; }

    /// <summary>The voucher charged (per-voucher row). Unique where not null. Null on a plan-level row.</summary>
    public Guid? VoucherDetailId { get; set; }

    /// <summary>The plan charged at approval (plan-level row). Unique where not null. Null on a per-voucher row.</summary>
    public Guid? PlanId { get; set; }

    /// <summary>Credits consumed by this row: 1 for a per-voucher charge, N for a plan-level approval charge.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Consumption context (e.g. "plan-approval", "gift-sold", "complimentary-redeemed").</summary>
    public string? Reference { get; set; }

    // Navigation
    public CreditBatch? Batch { get; set; }
    public Brand? Brand { get; set; }
}

using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Prepaid credit billing on the batch model (Epic 10).
/// Each top-up = one <see cref="CreditBatch"/> with its own price snapshot and expiry.
/// Balance = SUM(RemainingAmount) over non-expired batches; consumption drains FIFO
/// from the oldest non-expired batch. Consumption never throws and never blocks the
/// calling business operation (grace overdraft).
/// </summary>
public interface ICreditService
{
    /// <summary>Returns the brand's usable balance (non-expired batches).</summary>
    Task<int> GetBalanceAsync(Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>Returns true when the brand's usable balance is greater than zero.</summary>
    Task<bool> HasCreditAsync(Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes 1 credit for a voucher, FIFO from the oldest non-expired batch.
    /// Idempotent on voucherDetailId (1 voucher = max 1 credit). Never throws.
    /// </summary>
    Task TryConsumeAsync(Guid brandId, Guid voucherDetailId, string? reference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Purchase batch after admin verified the bank money-in.
    /// Price and expiry are snapshotted from the brand's resolved policy.
    /// </summary>
    Task<CreditBatch> CreatePurchaseAsync(Guid brandId, int amount, string? reference, string? evidenceImageUrl, Guid? byUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants the policy-defined welcome credits as a WelcomeGrant batch.
    /// No-op (returns null) when the policy grants 0 or the brand already has one.
    /// </summary>
    Task<CreditBatch?> GrantWelcomeAsync(Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a batch for an applied adjustment (Grant/Compensation/Correction/Clawback/Reinstatement).
    /// Clawback produces a negative batch. Intended for CreditAdjustmentService only.
    /// </summary>
    Task<CreditBatch> CreateAdjustmentBatchAsync(CreditAdjustmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns a paginated batch list with optional filters (admin/brand history view).</summary>
    Task<CreditBatchResult> GetBatchesAsync(CreditBatchFilters filters, CancellationToken cancellationToken = default);

    /// <summary>Returns a paginated consumption list for a brand.</summary>
    Task<CreditConsumptionResult> GetConsumptionsAsync(Guid brandId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>Returns batches with remaining credits expiring within the given window.</summary>
    Task<IReadOnlyList<CreditBatch>> GetExpiringBatchesAsync(Guid brandId, int withinDays, CancellationToken cancellationToken = default);
}

public class CreditBatchFilters
{
    public Guid? BrandId { get; set; }
    public CreditBatchType? BatchType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public record CreditBatchResult(
    IReadOnlyList<CreditBatch> Batches,
    int TotalCount,
    int Page,
    int PageSize);

public record CreditConsumptionResult(
    IReadOnlyList<CreditConsumption> Consumptions,
    int TotalCount,
    int Page,
    int PageSize);

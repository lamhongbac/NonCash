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
    /// Reusable funding check for a plan approval: can the brand fund <paramref name="requiredQuantity"/>
    /// credits right now? Used by both the approve-form pre-check and the approve action (double-check).
    /// </summary>
    Task<CreditFundingResult> EvaluatePlanFundingAsync(Guid brandId, int requiredQuantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges <paramref name="quantity"/> credits for a plan approval, FIFO across non-expired batches.
    /// Idempotent per planId (one plan-level charge per plan). Returns false — with no partial charge —
    /// when the balance is insufficient. Never throws.
    /// </summary>
    Task<bool> TryConsumeForPlanAsync(Guid brandId, Guid planId, int quantity, string? reference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Purchase batch after admin verified the bank money-in.
    /// Price and expiry are snapshotted from the brand's resolved policy.
    /// </summary>
    Task<CreditBatch> CreatePurchaseAsync(Guid brandId, int amount, string? reference, string? evidenceImageUrl, Guid? byUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants the policy-defined welcome credits as a WelcomeGrant batch.
    /// No-op (returns null) when the policy grants 0 or the brand already has one.
    /// Set <paramref name="sendNotification"/> to false when the caller sends its own email (e.g. business activation).
    /// </summary>
    Task<CreditBatch?> GrantWelcomeAsync(Guid brandId, bool sendNotification = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a batch for an applied adjustment (Grant/Compensation/Correction/Clawback/Reinstatement).
    /// Clawback produces a negative batch. Intended for CreditAdjustmentService only.
    /// </summary>
    Task<CreditBatch> CreateAdjustmentBatchAsync(CreditAdjustmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns a paginated batch list with optional filters (admin/brand history view).</summary>
    Task<CreditBatchResult> GetBatchesAsync(CreditBatchFilters filters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated consumption list for a brand, enriched with human-readable
    /// names (plan display name / voucher serial) for the history view, plus the
    /// all-time sum of consumed credits (not just the page).
    /// </summary>
    Task<CreditConsumptionResult> GetConsumptionsAsync(Guid brandId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// All-time ledger totals for a brand: credits granted (positive batches) and
    /// credits consumed (sum over consumption rows). Powers the reconciliation strip.
    /// </summary>
    Task<CreditLedgerTotals> GetLedgerTotalsAsync(Guid brandId, CancellationToken cancellationToken = default);

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

/// <summary>One consumption ledger row enriched with display names for the history view.</summary>
public record CreditConsumptionItem(
    CreditConsumption Consumption,
    string? PlanName,
    string? VoucherSerialNo);

public record CreditConsumptionResult(
    IReadOnlyList<CreditConsumptionItem> Consumptions,
    int TotalCount,
    int TotalQuantity,
    int Page,
    int PageSize);

/// <summary>All-time ledger totals: granted (positive batches) vs consumed (charge rows).</summary>
public record CreditLedgerTotals(int TotalGranted, int TotalConsumed);

/// <summary>Result of a plan-funding check: whether the brand can fund <see cref="Required"/> credits now.</summary>
public record CreditFundingResult(bool Sufficient, int Balance, int Required);

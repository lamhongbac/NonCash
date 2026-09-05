using System.Text.Json.Serialization;
using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Read-side queries for distribution batch runs and per-voucher traceability:
/// the brand manager can review the result of each batch distribute run and the
/// full lifecycle (distributed, redeeming, redeemed, expired) of every voucher a
/// customer received. All queries are brand-scoped; cross-brand access returns
/// empty/not-found rather than leaking data.
/// </summary>
public interface IDistributionBatchService
{
    /// <summary>All batch runs of one plan, newest first. Empty when the plan is unknown or belongs to another brand.</summary>
    Task<IReadOnlyList<DistributionBatchSummary>> GetBatchesByPlanAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>One batch run including skipped and delivered recipient rows. Null when unknown or cross-brand.</summary>
    Task<DistributionBatchDetail?> GetBatchDetailAsync(Guid batchId, Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>Full voucher ledger of a plan (in-stock + distributed) with recipient and lifecycle status per voucher.</summary>
    Task<IReadOnlyList<PlanVoucherLedgerRow>> GetPlanVoucherLedgerAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voucher history of one customer, restricted to the given brand's plans.
    /// Null when the customer is not mapped to the brand (caller returns 404).
    /// </summary>
    Task<IReadOnlyList<CustomerVoucherHistoryRow>?> GetCustomerVouchersAsync(Guid customerId, Guid brandId, CancellationToken cancellationToken = default);
}

public record DistributionBatchSummary(
    Guid Id,
    Guid PlanId,
    string PlanName,
    DateTime CreatedAt,
    string? CreatedByName,
    string NotifyChannel,
    int RecipientCount,
    int DistributedCount,
    int SkippedCount);

public record BatchRecipientRow(
    Guid VoucherId,
    string SerialNo,
    string? RecipientPhone,
    string? RecipientName,
    string Status,
    string Method,
    DateTime DistributedAt,
    DateTime? UsedDate);

public record DistributionBatchDetail(
    DistributionBatchSummary Summary,
    IReadOnlyList<BatchSkippedRecipient> SkippedRecords,
    IReadOnlyList<BatchRecipientRow> Recipients);

/// <summary>
/// One voucher in a plan ledger. <see cref="VoucherCodeSecret"/> is server-side only
/// (the API layer turns it into a short-lived dynamic code); it is never serialized.
/// </summary>
public record PlanVoucherLedgerRow(
    Guid Id,
    string SerialNo,
    string Status,
    DateTime? UsedDate,
    [property: JsonIgnore] string VoucherCodeSecret,
    string? RecipientPhone,
    string? RecipientName,
    string? DistributionMethod,
    DateTime? DistributedAt,
    Guid? BatchId);

/// <summary>
/// One voucher in a customer's brand-scoped history. <see cref="VoucherCodeSecret"/>
/// is server-side only (same dynamic-code handling as <see cref="PlanVoucherLedgerRow"/>).
/// </summary>
public record CustomerVoucherHistoryRow(
    Guid Id,
    string SerialNo,
    Guid PlanId,
    string PlanName,
    string VoucherType,
    decimal FaceValue,
    string Status,
    DateTime? DistributedAt,
    string? DistributionMethod,
    DateTime? UsedDate,
    DateTime ExpiryDate,
    [property: JsonIgnore] string VoucherCodeSecret);

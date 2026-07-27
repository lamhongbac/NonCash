using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Manages cross-tenant settlement ledger: creation, querying, manual settlement, and netting reports.
/// </summary>
public interface ISettlementService
{
    /// <summary>
    /// Creates a settlement entry from a VoucherUsage (called after POS commit when cross-tenant detected).
    /// Idempotent on VoucherUsageId.
    /// </summary>
    Task<SettlementEntry?> CreateSettlementEntryAsync(VoucherUsage usage, Guid issuingBrandId, decimal faceValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated list of settlement entries with optional filters.
    /// </summary>
    Task<SettlementLedgerResult> GetLedgerAsync(SettlementFilters filters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a pending settlement entry as settled.
    /// </summary>
    Task<bool> MarkSettledAsync(Guid entryId, Guid settledBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes net amounts between all sponsor/redeemer brand pairs within a date range.
    /// Returns a dictionary keyed by (SponsorBrandId, RedeemBrandId) → net decimal.
    /// </summary>
    Task<Dictionary<(Guid? SponsorBrandId, Guid? RedeemBrandId), decimal>> ComputeNettingAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

public class SettlementFilters
{
    public Guid? SponsorBrandId { get; set; }
    public Guid? RedeemBrandId { get; set; }
    public SettlementStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public record SettlementLedgerResult(
    IReadOnlyList<SettlementEntry> Entries,
    int TotalCount,
    int Page,
    int PageSize);

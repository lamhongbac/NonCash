using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Prepaid credit billing (Epic 9): balance queries, idempotent consumption,
/// manual top-ups, and ledger queries. Balance may go negative on consumption
/// (grace overdraft) — guards only block upstream actions, never POS redemption.
/// </summary>
public interface ICreditService
{
    /// <summary>Returns the brand's current credit balance (SUM of ledger amounts).</summary>
    Task<int> GetBalanceAsync(Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>Returns true when the brand's balance is greater than zero.</summary>
    Task<bool> HasCreditAsync(Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes 1 credit for a voucher. Idempotent on voucherDetailId (1 voucher = max 1 credit).
    /// Never throws and never blocks the calling business operation; balance may go negative.
    /// </summary>
    Task TryConsumeAsync(Guid brandId, Guid voucherDetailId, string? reference = null, CancellationToken cancellationToken = default);

    /// <summary>Adds a Grant, Purchase, or Adjustment entry (manual admin flow in v1).</summary>
    Task<CreditLedgerEntry> TopUpAsync(Guid brandId, int amount, CreditEntryType type, string? reference, Guid? byUserId, CancellationToken cancellationToken = default);

    /// <summary>Returns a paginated ledger with optional filters.</summary>
    Task<CreditLedgerResult> GetLedgerAsync(CreditLedgerFilters filters, CancellationToken cancellationToken = default);
}

public class CreditLedgerFilters
{
    public Guid? BrandId { get; set; }
    public CreditEntryType? EntryType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public record CreditLedgerResult(
    IReadOnlyList<CreditLedgerEntry> Entries,
    int TotalCount,
    int Page,
    int PageSize);

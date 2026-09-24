namespace NonCash.Core.Interfaces;

/// <summary>One committed redemption in the brand-scoped redemption report.</summary>
/// <param name="Breakage">FaceValue - AmountUsed for fixed-value plans; null for percentage
/// plans (FaceValue is a percent number there, so a VND breakage would be meaningless).</param>
public record RedemptionReportRow(
    Guid UsageId,
    DateTime UsageDate,
    string TransactionId,
    string? BillNumber,
    string SerialNo,
    string ValueType,
    decimal FaceValue,
    decimal AmountUsed,
    decimal? Breakage,
    Guid OutletId,
    string? OutletName,
    string? PosNo,
    string? OperatorId);

/// <summary>One page of redemption rows plus totals across the whole filtered set
/// (not just the current page), so the UI can show running sums under the filters.</summary>
/// <param name="TotalBreakage">Sum of breakage over fixed-value rows only; percentage rows never contribute.</param>
public record RedemptionReportPage(
    IReadOnlyList<RedemptionReportRow> Items,
    int TotalCount,
    int Page,
    int PageSize,
    decimal TotalFaceValue,
    decimal TotalAmountUsed,
    decimal TotalBreakage);

public record RedemptionOutletOption(Guid OutletId, string OutletName, string? OutletCode);

/// <summary>Read-only redemption report: committed voucher usages joined to their plan
/// (face value, value type) and the redeeming outlet. Only successful commits create
/// usage rows, so cancelled or rolled-back transactions never appear here.</summary>
public interface IRedemptionReportRepository
{
    Task<RedemptionReportPage> GetReportAsync(
        Guid? brandId,
        Guid? outletId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RedemptionOutletOption>> GetOutletsForBrandAsync(
        Guid brandId,
        CancellationToken cancellationToken = default);

    /// <summary>Every row matching the filters, newest first, with no paging — the CSV export
    /// must contain the same set the on-screen totals describe.</summary>
    Task<IReadOnlyList<RedemptionReportRow>> GetRowsForExportAsync(
        Guid? brandId,
        Guid? outletId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}

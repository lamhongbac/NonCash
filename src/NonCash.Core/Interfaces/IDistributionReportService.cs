namespace NonCash.Core.Interfaces;

public interface IDistributionReportService
{
    Task<DistributionSummary> GetSummaryAsync(
        Guid brandId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionDetailItem>> GetPlanDetailsAsync(
        Guid brandId,
        Guid planId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}

public record DistributionSummary(
    int TotalDistributed,
    MethodBreakdown TotalByMethod,
    IReadOnlyList<PlanDistributionRow> Plans);

public record MethodBreakdown(int Sale, int Promotion, int Transfer);

public record PlanDistributionRow(
    Guid PlanId,
    DateTime PlanDate,
    string VoucherType,
    decimal FaceValue,
    int TargetDistributed,
    int ActualDistributed,
    double Percentage,
    DateTime ExpiryDate,
    bool IsAtRisk,
    MethodBreakdown ByMethod);

public record DistributionDetailItem(
    Guid VoucherId,
    string SerialNo,
    string Method,
    DateTime DistributionDate,
    string? RecipientPhone,
    string? RecipientName);

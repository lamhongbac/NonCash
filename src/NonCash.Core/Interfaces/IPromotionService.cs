using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IPromotionService
{
    Task<PromotionResult> DistributeAsync(
        Guid planId,
        Guid brandId,
        IReadOnlyList<string> phoneNumbers,
        NotificationChannel notifyChannels = NotificationChannel.Email,
        CancellationToken cancellationToken = default);

    // Epic 6.3: Wallet & Event History for Integration API
    Task<IReadOnlyList<MemberWalletVoucher>> GetMemberVouchersByPhoneAsync(
        string phone, List<Guid> brandIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberEventRecord>> GetMemberEventsByPhoneAsync(
        string phone, List<Guid> brandIds, int limit, CancellationToken cancellationToken = default);

    // Epic 6.5: Campaign Performance
    Task<CampaignPerformanceResult?> GetCampaignPerformanceAsync(
        Guid planId, List<Guid> brandIds, CancellationToken cancellationToken = default);
}

public record PromotionResult(
    bool Success,
    int DistributedCount = 0,
    int SkippedCount = 0,
    IReadOnlyList<SkippedRecord>? SkippedRecords = null,
    IReadOnlyList<PromotionError>? Errors = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record SkippedRecord(string PhoneNumber, string Reason);

public record PromotionError(Guid MemberId, string Reason);

// Epic 6.3: DTOs for wallet and event history
public record MemberWalletVoucher(
    Guid VoucherId,
    string SerialNo,
    decimal FaceValue,
    object? ValueType,
    DateTime? ExpiryDate,
    object? UsageStatus,
    string? ImageUrl,
    string? IconUrl,
    string? CoverImageUrl,
    string? BrandColor,
    string? DisplayName,
    string? ShortDescription,
    string? TermsAndConditions,
    string? BrandName);

public record MemberEventRecord(
    string EventType,
    DateTime OccurredAt,
    Guid? VoucherId,
    string? SerialNo,
    string? BrandName,
    string? Details);

// Epic 6.5: Campaign performance
public record CampaignPerformanceResult(
    Guid PlanId,
    string? PlanName,
    int TotalVouchers,
    int DistributedCount,
    int RedeemedCount,
    decimal RedemptionRate,
    List<OutletPerformance> OutletBreakdown);

public record OutletPerformance(
    Guid OutletId,
    string? OutletName,
    int RedeemedCount,
    decimal TotalRedeemedValue);

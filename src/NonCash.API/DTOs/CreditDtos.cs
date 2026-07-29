namespace NonCash.API.DTOs;

public record CreditBalanceResponse(
    Guid BrandId,
    int Balance
);

public record CreditBatchDto(
    Guid Id,
    Guid BrandId,
    string? BrandName,
    string BatchType,
    int OriginalAmount,
    int RemainingAmount,
    decimal PricePerCreditVnd,
    decimal TotalPaidVnd,
    DateTime? ExpiresAt,
    string? EvidenceImageUrl,
    string? Reference,
    Guid? AdjustmentRequestId,
    Guid? CreatedBy,
    DateTime CreatedAt
);

public record CreditBatchListResponse(
    IReadOnlyList<CreditBatchDto> Batches,
    int TotalCount,
    int Page,
    int PageSize
);

public record CreditConsumptionDto(
    Guid Id,
    Guid BatchId,
    Guid VoucherDetailId,
    string? Reference,
    DateTime CreatedAt
);

public record CreditConsumptionListResponse(
    IReadOnlyList<CreditConsumptionDto> Consumptions,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>Purchase top-up: admin verified bank money-in first; evidence image required by flow.</summary>
public record CreditPurchaseRequest(
    Guid BrandId,
    int Amount,
    string? Reference,
    string? EvidenceImageUrl
);

/// <summary>Resolved pricing policy for a brand (Brand → Group → Global → config fallback).</summary>
public record ResolvedPolicyResponse(
    Guid? PolicyId,
    string Name,
    string? Scope,
    decimal PricePerCreditVnd,
    int? CreditExpiryMonths,
    int WelcomeCredits,
    int? WelcomeCreditExpiryMonths,
    int? LowBalanceWarningPct,
    int? ExpiryWarningDays,
    int? AdjustmentApprovalThreshold
);

// ----- Policy management (Admin) -----

public record CreditPolicyDto(
    Guid Id,
    string Name,
    string Scope,
    Guid? BrandGroupId,
    string? BrandGroupName,
    Guid? BrandId,
    string? BrandName,
    decimal PricePerCreditVnd,
    int? CreditExpiryMonths,
    int WelcomeCredits,
    int? WelcomeCreditExpiryMonths,
    int? LowBalanceWarningPct,
    int? ExpiryWarningDays,
    int? AdjustmentApprovalThreshold,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive,
    DateTime CreatedAt
);

public record SaveCreditPolicyRequest(
    string Name,
    string Scope,
    Guid? BrandGroupId,
    Guid? BrandId,
    decimal PricePerCreditVnd,
    int? CreditExpiryMonths,
    int WelcomeCredits,
    int? WelcomeCreditExpiryMonths,
    int? LowBalanceWarningPct,
    int? ExpiryWarningDays,
    int? AdjustmentApprovalThreshold,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive
);

// ----- Brand groups (Admin) -----

public record BrandGroupMemberDto(
    Guid BrandId,
    string? BrandName
);

public record BrandGroupDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<BrandGroupMemberDto> Members
);

public record SaveBrandGroupRequest(
    string Name,
    string? Description,
    bool IsActive = true
);

public record SetGroupMembersRequest(
    IReadOnlyList<Guid> BrandIds
);

// ----- Adjustments (maker-checker) -----

public record CreditAdjustmentDto(
    Guid Id,
    Guid BrandId,
    string? BrandName,
    string AdjustmentType,
    int Amount,
    Guid? RelatedBatchId,
    string ReasonText,
    string? EvidenceNote,
    string? EvidenceImageUrl,
    string Status,
    bool RequiresApproval,
    int? ApprovalThreshold,
    Guid RequestedBy,
    DateTime RequestedAt,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? ReviewNote,
    DateTime? AppliedAt
);

public record CreditAdjustmentListResponse(
    IReadOnlyList<CreditAdjustmentDto> Requests,
    int TotalCount,
    int Page,
    int PageSize
);

public record CreateAdjustmentRequest(
    Guid BrandId,
    string AdjustmentType,
    int Amount,
    Guid? RelatedBatchId,
    string ReasonText,
    string? EvidenceNote,
    string? EvidenceImageUrl
);

public record ReviewAdjustmentRequest(
    string? Note
);

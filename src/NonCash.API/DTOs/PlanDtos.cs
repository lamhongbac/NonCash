namespace NonCash.API.DTOs;

public record CreatePlanRequest(
    DateTime PlanDate,
    string VoucherType,
    string ValueType,
    decimal FaceValue,
    decimal NetValue,
    DateTime ExpiryDate,
    DateTime PublishDate,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    int TargetQuantity,
    decimal Budget,
    string? ImageUrl,
    string? IconUrl,
    List<Guid> OutletIds
);

public record UpdatePlanRequest(
    DateTime PlanDate,
    string VoucherType,
    string ValueType,
    decimal FaceValue,
    decimal NetValue,
    DateTime ExpiryDate,
    DateTime PublishDate,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    int TargetQuantity,
    decimal Budget,
    string? ImageUrl,
    string? IconUrl,
    List<Guid> OutletIds
);

public record PlanResponse(
    Guid Id,
    DateTime PlanDate,
    string VoucherType,
    string ValueType,
    decimal FaceValue,
    decimal NetValue,
    DateTime ExpiryDate,
    DateTime PublishDate,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    int TargetQuantity,
    decimal Budget,
    int TargetDistributed,
    int TargetUsed,
    string ApprovalStatus,
    string? ImageUrl,
    string? IconUrl,
    List<Guid> OutletIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int? VersionNumber = null,
    Guid? PreviousVersionId = null
);

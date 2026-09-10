namespace NonCash.API.DTOs;

public record CreateOutletRequest(
    string Name,
    string? Address,
    string? Code = null,
    string? RedemptionMode = null
);

public record UpdateOutletRequest(
    string Name,
    string? Address,
    string? Code = null,
    string? RedemptionMode = null
);

public record OutletResponse(
    Guid Id,
    Guid BrandId,
    string Name,
    string? Address,
    string? Code,
    string Status,
    string? ApiKeyPrefix,
    string RedemptionMode,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

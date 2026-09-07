namespace NonCash.API.DTOs;

public record CreateOutletRequest(
    string Name,
    string? Address,
    string? Code = null
);

public record UpdateOutletRequest(
    string Name,
    string? Address,
    string? Code = null
);

public record OutletResponse(
    Guid Id,
    Guid BrandId,
    string Name,
    string? Address,
    string? Code,
    string Status,
    string? ApiKeyPrefix,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

namespace NonCash.API.DTOs;

public record CreateOutletRequest(
    string Name,
    string? Address
);

public record UpdateOutletRequest(
    string Name,
    string? Address
);

public record OutletResponse(
    Guid Id,
    Guid BrandId,
    string Name,
    string? Address,
    string Status,
    string? ApiKeyPrefix,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

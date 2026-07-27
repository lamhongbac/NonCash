namespace NonCash.API.DTOs;

public record CreateBusinessRequest(
    string BusinessName,
    string TaxCode,
    string Address,
    string? ContactEmail,
    string? PhoneNumber
);

public record UpdateBusinessRequest(
    string BusinessName,
    string Address,
    string? ContactEmail,
    string? PhoneNumber,
    bool IsActive
);

public record BusinessResponse(
    Guid Id,
    string BusinessName,
    string TaxCode,
    string Address,
    string? ContactEmail,
    string? PhoneNumber,
    bool IsActive,
    int BrandCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

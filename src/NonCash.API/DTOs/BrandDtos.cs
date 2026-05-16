using NonCash.Core.Entities;

namespace NonCash.API.DTOs;

public record CreateBrandRequest(
    string Name,
    string TaxCode,
    string? ContactEmail
);

public record UpdateBrandRequest(
    string Name,
    string? ContactEmail,
    string Status
);

public record BrandResponse(
    Guid Id,
    string Name,
    string TaxCode,
    string? ContactEmail,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

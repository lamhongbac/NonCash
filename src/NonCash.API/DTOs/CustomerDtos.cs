namespace NonCash.API.DTOs;

public record CreateCustomerRequest(
    string PhoneNumber,
    string FullName,
    string? Email
);

public record UpdateCustomerRequest(
    string FullName,
    string? Email
);

public record CustomerResponse(
    Guid Id,
    string PhoneNumber,
    string FullName,
    string? Email,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CustomerImportResponse(
    int Created,
    int Updated,
    int ErrorCount,
    IReadOnlyList<string> Errors
);

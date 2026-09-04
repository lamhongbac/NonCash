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
    DateTime? UpdatedAt,
    bool? IsBrandBlocked = null
);

public record CustomerImportResponse(
    int Created,
    int Updated,
    int ErrorCount,
    IReadOnlyList<CustomerImportErrorDto> Errors
);

public record CustomerImportErrorDto(
    int Row,
    string PhoneNumber,
    string FullName,
    string? Email,
    string Message
);

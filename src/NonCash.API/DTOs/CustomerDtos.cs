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
    int Unchanged,
    int SkippedCount,
    IReadOnlyList<CustomerImportSkipDto> Skipped,
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

/// <summary>
/// A row whose incoming values were kept (not written) because brand writes are
/// fill-empty-only (CR-2026-09-06-11).
/// </summary>
public record CustomerImportSkipDto(
    int Row,
    string PhoneNumber,
    string FullName,
    string? Email,
    string Message
);

/// <summary>One admin-edit audit entry (CR-2026-09-07-14).</summary>
public record CustomerAuditDto(
    Guid Id,
    Guid CustomerId,
    Guid? ChangedByUserId,
    string Field,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAt
);

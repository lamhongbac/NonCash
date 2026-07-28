namespace NonCash.API.DTOs;

public record CreditBalanceResponse(
    Guid BrandId,
    int Balance
);

public record CreditLedgerEntryDto(
    Guid Id,
    Guid BrandId,
    string? BrandName,
    string EntryType,
    int Amount,
    string? Reference,
    Guid? VoucherDetailId,
    Guid? CreatedBy,
    DateTime CreatedAt
);

public record CreditLedgerResponse(
    IReadOnlyList<CreditLedgerEntryDto> Entries,
    int TotalCount,
    int Page,
    int PageSize
);

public record CreditTopUpRequest(
    Guid BrandId,
    int Amount,
    string Type,
    string? Reference
);

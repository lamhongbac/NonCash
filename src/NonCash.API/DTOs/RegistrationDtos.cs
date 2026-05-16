namespace NonCash.API.DTOs;

public record BusinessRegistrationRequest(
    string CompanyName,
    string TaxCode,
    string ContactEmail,
    string PhoneNumber,
    string Address,
    string RepresentativeName,
    string Username,
    string Password
);

public record BusinessRegistrationResponse(
    Guid RequestId,
    Guid BrandId,
    string Status
);

public record RegistrationStatusResponse(
    string Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes
);

public record AdminRegistrationRequestDto(
    Guid RequestId,
    string CompanyName,
    string TaxCode,
    string ContactEmail,
    string RepresentativeName,
    string Username,
    string Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    string? ReviewedByName
);

public record ReviewActionDto(
    string? ReviewNotes
);

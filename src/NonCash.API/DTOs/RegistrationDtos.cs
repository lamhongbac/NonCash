namespace NonCash.API.DTOs;

public record SubmitBusinessRegistrationRequest(
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
    Guid BusinessId,
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
    string BusinessName,
    string BrandName,
    string TaxCode,
    string ContactEmail,
    string RepresentativeName,
    string Username,
    string Status,
    string ContractStatus,
    DateTime? ContractSentAt,
    string? ContractFileUrl,
    Guid? WelcomePolicyTemplateId,
    string? WelcomePolicyTemplateName,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    string? ReviewedByName
);

public record ReviewActionDto(
    string? ReviewNotes
);

public record SendContractDto(
    Guid WelcomePolicyTemplateId
);

public record UploadSignedContractDto(
    string ContractFileUrl
);

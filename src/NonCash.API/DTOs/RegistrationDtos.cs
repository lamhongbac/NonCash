namespace NonCash.API.DTOs;

public class SubmitBusinessRegistrationRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RepresentativeName { get; set; } = string.Empty;
    public string? FirstBrandName { get; set; }
    public string? ManagerUsername { get; set; }
    public string? ManagerPassword { get; set; }
}

public record BusinessRegistrationResponse(
    Guid RequestId,
    string Status
);

public record RegistrationStatusResponse(
    string Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    bool HasFirstBrandDeclaration
);

public class AdminRegistrationRequestDto
{
    public Guid RequestId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string RepresentativeName { get; set; } = string.Empty;
    public string? FirstBrandName { get; set; }
    public string? ManagerUsername { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ContractStatus { get; set; } = string.Empty;
    public DateTime? ContractSentAt { get; set; }
    public string? ContractFileUrl { get; set; }
    public Guid? WelcomePolicyTemplateId { get; set; }
    public string? WelcomePolicyTemplateName { get; set; }
    public Guid? ContractTemplateId { get; set; }
    public string? ContractTemplateName { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ReviewedByName { get; set; }
    public bool HasFirstBrandDeclaration => !string.IsNullOrWhiteSpace(FirstBrandName);
}

public record ReviewActionDto(
    string? ReviewNotes
);

public record SendContractDto(
    Guid WelcomePolicyTemplateId
);

public record UploadSignedContractDto(
    string ContractFileUrl
);

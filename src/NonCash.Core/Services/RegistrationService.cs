using NonCash.Core.Configuration;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public interface IRegistrationService
{
    Task<RegistrationResult> SubmitAsync(RegistrationRequestDto request, CancellationToken cancellationToken = default);
    Task<RegistrationStatusInfo?> GetStatusAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistrationRequestSummary>> GetPendingReviewRequestsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistrationRequestSummary>> GetPendingContractRequestsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistrationRequestSummary>> GetAllRequestsAsync(CancellationToken cancellationToken = default);
    Task<ReviewResult> SendContractAsync(Guid requestId, Guid welcomePolicyTemplateId, Guid senderUserId, CancellationToken cancellationToken = default);
    Task<ReviewResult> UploadSignedContractAsync(Guid requestId, string contractFileUrl, Guid adminUserId, CancellationToken cancellationToken = default);
    Task<ReviewResult> ReviewAsync(Guid requestId, Guid reviewerUserId, bool approve, string? reviewNotes, CancellationToken cancellationToken = default);
}

public record RegistrationRequestDto(
    string CompanyName,
    string TaxCode,
    string ContactEmail,
    string PhoneNumber,
    string Address,
    string RepresentativeName,
    string? FirstBrandName = null,
    string? ManagerUsername = null,
    string? ManagerPassword = null
);

public record RegistrationResult
{
    public bool Success { get; init; }
    public Guid? RequestId { get; init; }
    public Guid? BusinessId { get; init; }
    public Guid? BrandId { get; init; }
    public RegistrationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public RegistrationResult(bool success, string? errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
        Status = RegistrationStatus.Submitted;
    }

    public RegistrationResult(bool success, Guid requestId, RegistrationStatus status)
    {
        Success = success;
        RequestId = requestId;
        Status = status;
    }
}

public record RegistrationStatusInfo(
    RegistrationStatus Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    bool HasFirstBrandDeclaration
);

public record RegistrationRequestSummary(
    Guid RequestId,
    Guid? BusinessId,
    Guid? BrandId,
    Guid? SubmittedByUserId,
    string BusinessName,
    string? BrandName,
    string TaxCode,
    string? ContactEmail,
    string? PhoneNumber,
    string? Address,
    string RepresentativeName,
    string? FirstBrandName,
    string? ManagerUsername,
    bool HasFirstBrandDeclaration,
    RegistrationStatus Status,
    ContractStatus ContractStatus,
    DateTime? ContractSentAt,
    string? ContractFileUrl,
    Guid? WelcomePolicyTemplateId,
    string? WelcomePolicyTemplateName,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    string? ReviewedByName
);

public record ReviewResult(bool Success, string? ErrorMessage = null);

public class RegistrationService : IRegistrationService
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IBusinessRegistrationRequestRepository _requestRepository;
    private readonly IAuthService _authService;
    private readonly INotificationService _notificationService;
    private readonly ICreditService? _creditService;
    private readonly IWelcomePolicyService? _welcomePolicyService;
    private readonly IContractService? _contractService;
    private readonly CreditConfig _creditConfig;

    public RegistrationService(
        IBusinessRepository businessRepository,
        IBrandRepository brandRepository,
        IUserAccountRepository userAccountRepository,
        IBusinessRegistrationRequestRepository requestRepository,
        IAuthService authService,
        INotificationService notificationService,
        ICreditService? creditService = null,
        IWelcomePolicyService? welcomePolicyService = null,
        IContractService? contractService = null,
        CreditConfig? creditConfig = null)
    {
        _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
        _brandRepository = brandRepository ?? throw new ArgumentNullException(nameof(brandRepository));
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _creditService = creditService;
        _welcomePolicyService = welcomePolicyService;
        _contractService = contractService;
        _creditConfig = creditConfig ?? new CreditConfig();
    }

    public async Task<RegistrationResult> SubmitAsync(RegistrationRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
            return new RegistrationResult(false, "Company name is required.");
        if (string.IsNullOrWhiteSpace(request.TaxCode))
            return new RegistrationResult(false, "Tax code is required.");
        if (string.IsNullOrWhiteSpace(request.RepresentativeName))
            return new RegistrationResult(false, "Representative name is required.");

        var hasFirstBrand = !string.IsNullOrWhiteSpace(request.FirstBrandName);
        if (hasFirstBrand)
        {
            if (string.IsNullOrWhiteSpace(request.ManagerUsername))
                return new RegistrationResult(false, "Manager username is required when first brand is declared.");
            if (string.IsNullOrWhiteSpace(request.ManagerPassword) || request.ManagerPassword.Length < 8)
                return new RegistrationResult(false, "Manager password must be at least 8 characters.");
        }

        // Check tax code uniqueness against existing businesses
        var existingBusiness = await _businessRepository.GetByTaxCodeAsync(request.TaxCode.Trim(), cancellationToken);
        if (existingBusiness != null)
            return new RegistrationResult(false, "DuplicateTaxCode");

        // Check tax code uniqueness against pending requests
        var pendingRequestWithSameTaxCode = (await _requestRepository.FindAsync(
            r => r.TaxCode == request.TaxCode.Trim() && r.Status == RegistrationStatus.Submitted,
            cancellationToken)).FirstOrDefault();
        if (pendingRequestWithSameTaxCode != null)
            return new RegistrationResult(false, "DuplicateTaxCode");

        // Check username uniqueness (only when a first brand is declared)
        if (hasFirstBrand && !string.IsNullOrWhiteSpace(request.ManagerUsername))
        {
            if (await _userAccountRepository.UsernameExistsAsync(request.ManagerUsername.Trim().ToLowerInvariant(), cancellationToken))
                return new RegistrationResult(false, "Username already exists.");

            // Also check against pending requests to avoid collisions before approval
            var pendingRequestWithSameUsername = (await _requestRepository.FindAsync(
                r => r.ManagerUsername == request.ManagerUsername.Trim().ToLowerInvariant() && r.Status == RegistrationStatus.Submitted,
                cancellationToken)).FirstOrDefault();
            if (pendingRequestWithSameUsername != null)
                return new RegistrationResult(false, "Username already exists.");
        }

        // Create registration request only; Business/Brand/UserAccount are created on approval.
        var registrationRequest = new BusinessRegistrationRequest
        {
            BusinessName = request.CompanyName.Trim(),
            TaxCode = request.TaxCode.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Address = request.Address?.Trim(),
            RepresentativeName = request.RepresentativeName.Trim(),
            FirstBrandName = hasFirstBrand ? request.FirstBrandName!.Trim() : null,
            ManagerUsername = hasFirstBrand ? request.ManagerUsername!.Trim().ToLowerInvariant() : null,
            ManagerPasswordHash = hasFirstBrand ? _authService.HashPassword(request.ManagerPassword!) : null,
            SubmittedAt = DateTime.UtcNow,
            Status = RegistrationStatus.Submitted,
            ContractStatus = ContractStatus.None
        };
        await _requestRepository.AddAsync(registrationRequest, cancellationToken);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        // Notify admins
        await _notificationService.NotifyAdminNewRegistrationAsync(
            registrationRequest.Id, registrationRequest.BusinessName, cancellationToken);

        // Send thank-you email to the applicant
        if (!string.IsNullOrWhiteSpace(request.ContactEmail))
        {
            await _notificationService.NotifyApplicantRegistrationSubmittedAsync(
                request.ContactEmail, registrationRequest.BusinessName, registrationRequest.Id, cancellationToken);
        }

        return new RegistrationResult(true, registrationRequest.Id, RegistrationStatus.Submitted);
    }

    public async Task<RegistrationStatusInfo?> GetStatusAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) return null;

        return new RegistrationStatusInfo(
            request.Status,
            request.SubmittedAt,
            request.ReviewedAt,
            request.ReviewNotes,
            !string.IsNullOrWhiteSpace(request.FirstBrandName)
        );
    }

    public async Task<IReadOnlyList<RegistrationRequestSummary>> GetPendingReviewRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.FindAsync(
            r => r.Status == RegistrationStatus.Submitted && r.ContractStatus == ContractStatus.Signed,
            cancellationToken);
        return await BuildSummariesAsync(requests, cancellationToken);
    }

    public async Task<IReadOnlyList<RegistrationRequestSummary>> GetPendingContractRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.FindAsync(
            r => r.Status == RegistrationStatus.Submitted && r.ContractStatus != ContractStatus.Signed,
            cancellationToken);
        return await BuildSummariesAsync(requests, cancellationToken);
    }

    public async Task<IReadOnlyList<RegistrationRequestSummary>> GetAllRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.GetAllAsync(cancellationToken);
        return await BuildSummariesAsync(requests, cancellationToken);
    }

    public async Task<ReviewResult> SendContractAsync(
        Guid requestId,
        Guid welcomePolicyTemplateId,
        Guid senderUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null)
            return new ReviewResult(false, "Registration request not found.");

        if (request.Status != RegistrationStatus.Submitted)
            return new ReviewResult(false, "This request has already been reviewed.");

        if (_welcomePolicyService == null)
            return new ReviewResult(false, "Welcome policy service is not available.");

        if (_contractService == null)
            return new ReviewResult(false, "Contract service is not available.");

        var template = await _welcomePolicyService.GetTemplateAsync(welcomePolicyTemplateId, cancellationToken);
        if (template == null || !template.IsActive)
            return new ReviewResult(false, "Welcome policy template not found or inactive.");

        request.WelcomePolicyTemplateId = welcomePolicyTemplateId;
        request.ContractStatus = ContractStatus.Sent;
        request.ContractSentAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        var contractHtml = await _contractService.GenerateContractHtmlAsync(
            request.BusinessName,
            request.FirstBrandName ?? string.Empty,
            request.TaxCode,
            request.RepresentativeName,
            template.Name,
            template.WelcomeCredits,
            template.WelcomeCreditExpiryMonths,
            cancellationToken);

        // Notify applicant that contract has been sent.
        await _notificationService.NotifyContractSentAsync(
            new ContractSentNotification(
                request.ContactEmail,
                request.BusinessName,
                request.FirstBrandName ?? string.Empty,
                template.Name,
                template.WelcomeCredits,
                template.WelcomeCreditExpiryMonths,
                contractHtml),
            cancellationToken);

        return new ReviewResult(true);
    }

    public async Task<ReviewResult> UploadSignedContractAsync(
        Guid requestId,
        string contractFileUrl,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contractFileUrl))
            return new ReviewResult(false, "Contract file URL is required.");

        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null)
            return new ReviewResult(false, "Registration request not found.");

        if (request.Status != RegistrationStatus.Submitted)
            return new ReviewResult(false, "This request has already been reviewed.");

        if (request.ContractStatus != ContractStatus.Sent)
            return new ReviewResult(false, "Contract must be sent before uploading the signed copy.");

        request.ContractFileUrl = contractFileUrl;
        request.ContractStatus = ContractStatus.Signed;
        request.UpdatedAt = DateTime.UtcNow;
        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        return new ReviewResult(true);
    }

    public async Task<ReviewResult> ReviewAsync(
        Guid requestId,
        Guid reviewerUserId,
        bool approve,
        string? reviewNotes,
        CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null)
            return new ReviewResult(false, "Registration request not found.");

        if (request.Status != RegistrationStatus.Submitted)
            return new ReviewResult(false, "This request has already been reviewed.");

        if (approve && request.ContractStatus != ContractStatus.Signed)
            return new ReviewResult(false, "Signed contract must be uploaded before approval.");

        Business? business = null;
        Brand? brand = null;
        UserAccount? user = null;
        CreditBatch? welcomeBatch = null;

        if (approve)
        {
            // Create the Business record on approval.
            business = new Business
            {
                BusinessName = request.BusinessName,
                TaxCode = request.TaxCode,
                Address = request.Address ?? string.Empty,
                ContactEmail = request.ContactEmail,
                PhoneNumber = request.PhoneNumber,
                IsActive = true
            };
            await _businessRepository.AddAsync(business, cancellationToken);
            await _businessRepository.SaveChangesAsync(cancellationToken);
            request.BusinessId = business.Id;

            // Assign the selected/default welcome policy template to the business.
            if (_welcomePolicyService != null)
            {
                await _welcomePolicyService.AssignTemplateToBusinessAsync(
                    business.Id, request.WelcomePolicyTemplateId, reviewerUserId, cancellationToken);
            }

            // Create the first Brand and its manager if the applicant declared them.
            if (!string.IsNullOrWhiteSpace(request.FirstBrandName) &&
                !string.IsNullOrWhiteSpace(request.ManagerUsername) &&
                !string.IsNullOrWhiteSpace(request.ManagerPasswordHash))
            {
                brand = new Brand
                {
                    BusinessId = business.Id,
                    Name = request.FirstBrandName,
                    TaxCode = request.TaxCode,
                    ContactEmail = request.ContactEmail,
                    Status = BrandStatus.Active
                };
                await _brandRepository.AddAsync(brand, cancellationToken);
                await _brandRepository.SaveChangesAsync(cancellationToken);
                request.BrandId = brand.Id;

                user = new UserAccount
                {
                    Username = request.ManagerUsername,
                    PasswordHash = request.ManagerPasswordHash,
                    FullName = request.RepresentativeName,
                    Role = UserRole.BrandManager,
                    BrandId = brand.Id,
                    Email = request.ContactEmail,
                    Status = UserStatus.Active
                };
                await _userAccountRepository.AddAsync(user, cancellationToken);
                await _userAccountRepository.SaveChangesAsync(cancellationToken);
                request.SubmittedByUserId = user.Id;

                // Grant welcome credits to the newly activated brand (policy-driven).
                if (_creditService != null)
                {
                    welcomeBatch = await _creditService.GrantWelcomeAsync(brand.Id, sendNotification: false, cancellationToken);
                }
            }
        }

        // Update the registration request
        request.Status = approve ? RegistrationStatus.Approved : RegistrationStatus.Rejected;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewNotes = reviewNotes?.Trim();
        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        // Notify the applicant: exactly one email per outcome.
        if (approve)
        {
            await _notificationService.NotifyBusinessActivatedAsync(new BusinessActivatedNotification(
                request.ContactEmail,
                request.BusinessName,
                brand?.Name ?? string.Empty,
                welcomeBatch?.OriginalAmount ?? 0,
                welcomeBatch?.ExpiresAt), cancellationToken);
        }
        else
        {
            await _notificationService.NotifyRegistrationRejectedAsync(
                request.ContactEmail ?? string.Empty,
                request.BusinessName,
                request.ReviewNotes,
                cancellationToken);
        }

        return new ReviewResult(true);
    }

    private async Task<IReadOnlyList<RegistrationRequestSummary>> BuildSummariesAsync(
        IEnumerable<BusinessRegistrationRequest> requests, CancellationToken cancellationToken)
    {
        var summaries = new List<RegistrationRequestSummary>();
        foreach (var r in requests)
        {
            string? reviewedByName = null;
            if (r.ReviewedByUserId.HasValue)
            {
                var reviewer = await _userAccountRepository.GetByIdAsync(r.ReviewedByUserId.Value, cancellationToken);
                reviewedByName = reviewer?.FullName;
            }

            summaries.Add(new RegistrationRequestSummary(
                r.Id,
                r.BusinessId,
                r.BrandId,
                r.SubmittedByUserId,
                r.BusinessName,
                r.FirstBrandName,
                r.TaxCode,
                r.ContactEmail,
                r.PhoneNumber,
                r.Address,
                r.RepresentativeName,
                r.FirstBrandName,
                r.ManagerUsername,
                !string.IsNullOrWhiteSpace(r.FirstBrandName),
                r.Status,
                r.ContractStatus,
                r.ContractSentAt,
                r.ContractFileUrl,
                r.WelcomePolicyTemplateId,
                r.WelcomePolicyTemplate?.Name,
                r.SubmittedAt,
                r.ReviewedAt,
                r.ReviewNotes,
                reviewedByName
            ));
        }
        return summaries;
    }
}

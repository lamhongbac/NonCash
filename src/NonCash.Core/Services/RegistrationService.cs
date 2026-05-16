using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public interface IRegistrationService
{
    Task<RegistrationResult> SubmitAsync(RegistrationRequestDto request, CancellationToken cancellationToken = default);
    Task<RegistrationStatusInfo?> GetStatusAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistrationRequestSummary>> GetPendingRequestsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistrationRequestSummary>> GetAllRequestsAsync(CancellationToken cancellationToken = default);
    Task<ReviewResult> ReviewAsync(Guid requestId, Guid reviewerUserId, bool approve, string? reviewNotes, CancellationToken cancellationToken = default);
}

public record RegistrationRequestDto(
    string CompanyName,
    string TaxCode,
    string ContactEmail,
    string PhoneNumber,
    string Address,
    string RepresentativeName,
    string Username,
    string Password
);

public record RegistrationResult
{
    public bool Success { get; init; }
    public Guid? RequestId { get; init; }
    public Guid? BrandId { get; init; }
    public RegistrationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public RegistrationResult(bool success, string? errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
        Status = RegistrationStatus.Submitted;
    }

    public RegistrationResult(bool success, Guid requestId, Guid brandId, RegistrationStatus status)
    {
        Success = success;
        RequestId = requestId;
        BrandId = brandId;
        Status = status;
    }
}

public record RegistrationStatusInfo(
    RegistrationStatus Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes
);

public record RegistrationRequestSummary(
    Guid RequestId,
    string CompanyName,
    string TaxCode,
    string ContactEmail,
    string RepresentativeName,
    string Username,
    RegistrationStatus Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    string? ReviewedByName
);

public record ReviewResult(bool Success, string? ErrorMessage = null);

public class RegistrationService : IRegistrationService
{
    private readonly IBrandRepository _brandRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IBrandRegistrationRequestRepository _requestRepository;
    private readonly IAuthService _authService;
    private readonly INotificationService _notificationService;

    public RegistrationService(
        IBrandRepository brandRepository,
        IUserAccountRepository userAccountRepository,
        IBrandRegistrationRequestRepository requestRepository,
        IAuthService authService,
        INotificationService notificationService)
    {
        _brandRepository = brandRepository ?? throw new ArgumentNullException(nameof(brandRepository));
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    public async Task<RegistrationResult> SubmitAsync(RegistrationRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
            return new RegistrationResult(false, "Company name is required.");
        if (string.IsNullOrWhiteSpace(request.TaxCode))
            return new RegistrationResult(false, "Tax code is required.");
        if (string.IsNullOrWhiteSpace(request.Username))
            return new RegistrationResult(false, "Username is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return new RegistrationResult(false, "Password must be at least 8 characters.");
        if (string.IsNullOrWhiteSpace(request.RepresentativeName))
            return new RegistrationResult(false, "Representative name is required.");

        // Check tax code uniqueness against Active or PendingActivation brands
        var existingBrand = await _brandRepository.GetByTaxCodeAsync(request.TaxCode.Trim(), cancellationToken);
        if (existingBrand != null && existingBrand.Status != BrandStatus.Suspended)
        {
            return new RegistrationResult(false, "DuplicateTaxCode");
        }

        // Check username uniqueness
        if (await _userAccountRepository.UsernameExistsAsync(request.Username.Trim().ToLowerInvariant(), cancellationToken))
        {
            return new RegistrationResult(false, "Username already exists.");
        }

        // Create Brand with PendingActivation status
        var brand = new Brand
        {
            Name = request.CompanyName.Trim(),
            TaxCode = request.TaxCode.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            Status = BrandStatus.PendingActivation
        };
        await _brandRepository.AddAsync(brand, cancellationToken);
        await _brandRepository.SaveChangesAsync(cancellationToken);

        // Create UserAccount for the representative with PendingActivation status
        var user = new UserAccount
        {
            Username = request.Username.Trim().ToLowerInvariant(),
            PasswordHash = _authService.HashPassword(request.Password),
            FullName = request.RepresentativeName.Trim(),
            Role = UserRole.BrandManager,
            BrandId = brand.Id,
            Status = UserStatus.PendingActivation
        };
        await _userAccountRepository.AddAsync(user, cancellationToken);
        await _userAccountRepository.SaveChangesAsync(cancellationToken);

        // Create registration request
        var registrationRequest = new BrandRegistrationRequest
        {
            BrandId = brand.Id,
            SubmittedByUserId = user.Id,
            SubmittedAt = DateTime.UtcNow,
            Status = RegistrationStatus.Submitted
        };
        await _requestRepository.AddAsync(registrationRequest, cancellationToken);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        // Notify admins
        await _notificationService.NotifyAdminNewRegistrationAsync(
            registrationRequest.Id, brand.Name, cancellationToken);

        return new RegistrationResult(true, registrationRequest.Id, brand.Id, RegistrationStatus.Submitted);
    }

    public async Task<RegistrationStatusInfo?> GetStatusAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) return null;

        return new RegistrationStatusInfo(
            request.Status,
            request.SubmittedAt,
            request.ReviewedAt,
            request.ReviewNotes
        );
    }

    public async Task<IReadOnlyList<RegistrationRequestSummary>> GetPendingRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.FindAsync(r => r.Status == RegistrationStatus.Submitted, cancellationToken);
        return await BuildSummariesAsync(requests, cancellationToken);
    }

    public async Task<IReadOnlyList<RegistrationRequestSummary>> GetAllRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.GetAllAsync(cancellationToken);
        return await BuildSummariesAsync(requests, cancellationToken);
    }

    public async Task<ReviewResult> ReviewAsync(Guid requestId, Guid reviewerUserId, bool approve, string? reviewNotes, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null)
            return new ReviewResult(false, "Registration request not found.");

        if (request.Status != RegistrationStatus.Submitted)
            return new ReviewResult(false, "This request has already been reviewed.");

        // Update the registration request
        request.Status = approve ? RegistrationStatus.Approved : RegistrationStatus.Rejected;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewNotes = reviewNotes?.Trim();
        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        // Update the associated Brand
        var brand = await _brandRepository.GetByIdAsync(request.BrandId, cancellationToken);
        if (brand != null)
        {
            brand.Status = approve ? BrandStatus.Active : BrandStatus.Suspended;
            _brandRepository.Update(brand);
            await _brandRepository.SaveChangesAsync(cancellationToken);
        }

        // Update the associated UserAccount
        var user = await _userAccountRepository.GetByIdAsync(request.SubmittedByUserId, cancellationToken);
        if (user != null)
        {
            user.Status = approve ? UserStatus.Active : UserStatus.Locked;
            _userAccountRepository.Update(user);
            await _userAccountRepository.SaveChangesAsync(cancellationToken);
        }

        // Notify the applicant
        await _notificationService.NotifyApplicantReviewResultAsync(
            request.SubmittedByUserId, brand?.Name ?? "", approve, cancellationToken);

        return new ReviewResult(true);
    }

    private async Task<IReadOnlyList<RegistrationRequestSummary>> BuildSummariesAsync(
        IEnumerable<BrandRegistrationRequest> requests, CancellationToken cancellationToken)
    {
        var summaries = new List<RegistrationRequestSummary>();
        foreach (var r in requests)
        {
            var brand = await _brandRepository.GetByIdAsync(r.BrandId, cancellationToken);
            var user = await _userAccountRepository.GetByIdAsync(r.SubmittedByUserId, cancellationToken);
            string? reviewedByName = null;
            if (r.ReviewedByUserId.HasValue)
            {
                var reviewer = await _userAccountRepository.GetByIdAsync(r.ReviewedByUserId.Value, cancellationToken);
                reviewedByName = reviewer?.FullName;
            }

            summaries.Add(new RegistrationRequestSummary(
                r.Id,
                brand?.Name ?? "",
                brand?.TaxCode ?? "",
                brand?.ContactEmail ?? "",
                user?.FullName ?? "",
                user?.Username ?? "",
                r.Status,
                r.SubmittedAt,
                r.ReviewedAt,
                r.ReviewNotes,
                reviewedByName
            ));
        }
        return summaries;
    }
}

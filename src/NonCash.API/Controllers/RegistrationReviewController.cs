using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/admin/registration-requests")]
[Authorize(Roles = "Admin")]
public class RegistrationReviewController : ControllerBase
{
    private readonly IRegistrationService _registrationService;
    private readonly IContractService _contractService;
    private readonly IBusinessRepository _businessRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IWelcomePolicyService _welcomePolicyService;

    public RegistrationReviewController(
        IRegistrationService registrationService,
        IContractService contractService,
        IBusinessRepository businessRepository,
        IBrandRepository brandRepository,
        IUserAccountRepository userAccountRepository,
        IWelcomePolicyService welcomePolicyService)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
        _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
        _brandRepository = brandRepository ?? throw new ArgumentNullException(nameof(brandRepository));
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _welcomePolicyService = welcomePolicyService ?? throw new ArgumentNullException(nameof(welcomePolicyService));
    }

    [HttpGet("pending-review")]
    public async Task<ActionResult<IReadOnlyList<AdminRegistrationRequestDto>>> GetPendingReview(CancellationToken cancellationToken)
    {
        var requests = await _registrationService.GetPendingReviewRequestsAsync(cancellationToken);
        return Ok(requests.Select(MapToDto));
    }

    [HttpGet("pending-contract")]
    public async Task<ActionResult<IReadOnlyList<AdminRegistrationRequestDto>>> GetPendingContract(CancellationToken cancellationToken)
    {
        var requests = await _registrationService.GetPendingContractRequestsAsync(cancellationToken);
        return Ok(requests.Select(MapToDto));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminRegistrationRequestDto>>> GetAll(CancellationToken cancellationToken)
    {
        var requests = await _registrationService.GetAllRequestsAsync(cancellationToken);
        return Ok(requests.Select(MapToDto));
    }

    [HttpGet("{requestId:guid}/contract")]
    public async Task<ActionResult> GetContractHtml(Guid requestId, CancellationToken cancellationToken)
    {
        var request = (await _registrationService.GetAllRequestsAsync(cancellationToken))
            .FirstOrDefault(r => r.RequestId == requestId);

        if (request == null)
            return NotFound(new { error = "Registration request not found." });

        if (!request.WelcomePolicyTemplateId.HasValue)
            return BadRequest(new { error = "No welcome policy template selected for this request." });

        var template = await _welcomePolicyService.GetTemplateAsync(request.WelcomePolicyTemplateId.Value, cancellationToken);
        if (template == null)
            return NotFound(new { error = "Welcome policy template not found." });

        var brand = await _brandRepository.GetByIdAsync(request.BrandId, cancellationToken);
        var business = brand != null ? await _businessRepository.GetByIdAsync(brand.BusinessId, cancellationToken) : null;
        var representative = await _userAccountRepository.GetByIdAsync(request.SubmittedByUserId, cancellationToken);

        var contractHtml = await _contractService.GenerateContractHtmlAsync(
            business?.BusinessName ?? brand?.Name ?? "",
            brand?.Name ?? "",
            business?.TaxCode ?? brand?.TaxCode ?? "",
            representative?.FullName,
            template.Name,
            template.WelcomeCredits,
            template.WelcomeCreditExpiryMonths,
            cancellationToken);

        return Content(contractHtml, "text/html");
    }

    [HttpPost("{requestId:guid}/send-contract")]
    public async Task<ActionResult> SendContract(Guid requestId, [FromBody] SendContractDto dto, CancellationToken cancellationToken)
    {
        var senderUserId = GetUserId();
        var result = await _registrationService.SendContractAsync(
            requestId, dto.WelcomePolicyTemplateId, senderUserId, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Contract sent successfully." });
    }

    [HttpPost("{requestId:guid}/upload-signed-contract")]
    public async Task<ActionResult> UploadSignedContract(Guid requestId, [FromBody] UploadSignedContractDto dto, CancellationToken cancellationToken)
    {
        var adminUserId = GetUserId();
        var result = await _registrationService.UploadSignedContractAsync(
            requestId, dto.ContractFileUrl, adminUserId, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Signed contract uploaded successfully." });
    }

    [HttpPost("{requestId:guid}/approve")]
    public async Task<ActionResult> Approve(Guid requestId, [FromBody] ReviewActionDto? dto, CancellationToken cancellationToken)
    {
        var reviewerUserId = GetUserId();
        var result = await _registrationService.ReviewAsync(
            requestId, reviewerUserId, true, dto?.ReviewNotes, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Registration approved successfully." });
    }

    [HttpPost("{requestId:guid}/reject")]
    public async Task<ActionResult> Reject(Guid requestId, [FromBody] ReviewActionDto? dto, CancellationToken cancellationToken)
    {
        var reviewerUserId = GetUserId();
        var result = await _registrationService.ReviewAsync(
            requestId, reviewerUserId, false, dto?.ReviewNotes, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Registration rejected." });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    private static AdminRegistrationRequestDto MapToDto(RegistrationRequestSummary s) => new(
        s.RequestId,
        s.BusinessName,
        s.BrandName,
        s.TaxCode,
        s.ContactEmail,
        s.RepresentativeName,
        s.Username,
        s.Status.ToString(),
        s.ContractStatus.ToString(),
        s.ContractSentAt,
        s.ContractFileUrl,
        s.WelcomePolicyTemplateId,
        s.WelcomePolicyTemplateName,
        s.SubmittedAt,
        s.ReviewedAt,
        s.ReviewNotes,
        s.ReviewedByName
    );
}

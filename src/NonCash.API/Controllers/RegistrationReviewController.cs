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
    private readonly IWelcomePolicyService _welcomePolicyService;

    public RegistrationReviewController(
        IRegistrationService registrationService,
        IContractService contractService,
        IWelcomePolicyService welcomePolicyService)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
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

        var contractHtml = await _contractService.GenerateContractHtmlAsync(
            request.BusinessName,
            request.FirstBrandName ?? string.Empty,
            request.TaxCode,
            request.RepresentativeName,
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

    private static AdminRegistrationRequestDto MapToDto(RegistrationRequestSummary s) => new()
    {
        RequestId = s.RequestId,
        BusinessName = s.BusinessName,
        TaxCode = s.TaxCode,
        ContactEmail = s.ContactEmail,
        PhoneNumber = s.PhoneNumber,
        Address = s.Address,
        RepresentativeName = s.RepresentativeName,
        FirstBrandName = s.FirstBrandName,
        ManagerUsername = s.ManagerUsername,
        Status = s.Status.ToString(),
        ContractStatus = s.ContractStatus.ToString(),
        ContractSentAt = s.ContractSentAt,
        ContractFileUrl = s.ContractFileUrl,
        WelcomePolicyTemplateId = s.WelcomePolicyTemplateId,
        WelcomePolicyTemplateName = s.WelcomePolicyTemplateName,
        SubmittedAt = s.SubmittedAt,
        ReviewedAt = s.ReviewedAt,
        ReviewNotes = s.ReviewNotes,
        ReviewedByName = s.ReviewedByName
    };
}

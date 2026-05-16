using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/admin/registration-requests")]
[Authorize(Roles = "Admin")]
public class RegistrationReviewController : ControllerBase
{
    private readonly IRegistrationService _registrationService;

    public RegistrationReviewController(IRegistrationService registrationService)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<AdminRegistrationRequestDto>>> GetPending(CancellationToken cancellationToken)
    {
        var requests = await _registrationService.GetPendingRequestsAsync(cancellationToken);
        return Ok(requests.Select(MapToDto));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminRegistrationRequestDto>>> GetAll(CancellationToken cancellationToken)
    {
        var requests = await _registrationService.GetAllRequestsAsync(cancellationToken);
        return Ok(requests.Select(MapToDto));
    }

    [HttpPost("{requestId:guid}/approve")]
    public async Task<ActionResult> Approve(Guid requestId, [FromBody] ReviewActionDto? dto, CancellationToken cancellationToken)
    {
        var reviewerUserId = GetUserId();
        var result = await _registrationService.ReviewAsync(requestId, reviewerUserId, true, dto?.ReviewNotes, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Registration approved successfully." });
    }

    [HttpPost("{requestId:guid}/reject")]
    public async Task<ActionResult> Reject(Guid requestId, [FromBody] ReviewActionDto? dto, CancellationToken cancellationToken)
    {
        var reviewerUserId = GetUserId();
        var result = await _registrationService.ReviewAsync(requestId, reviewerUserId, false, dto?.ReviewNotes, cancellationToken);

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
        s.CompanyName,
        s.TaxCode,
        s.ContactEmail,
        s.RepresentativeName,
        s.Username,
        s.Status.ToString(),
        s.SubmittedAt,
        s.ReviewedAt,
        s.ReviewNotes,
        s.ReviewedByName
    );
}

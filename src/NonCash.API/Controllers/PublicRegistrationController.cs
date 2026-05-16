using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/public")]
public class PublicRegistrationController : ControllerBase
{
    private readonly IRegistrationService _registrationService;

    public PublicRegistrationController(IRegistrationService registrationService)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<BusinessRegistrationResponse>> Register(
        BusinessRegistrationRequest request, CancellationToken cancellationToken)
    {
        var dto = new RegistrationRequestDto(
            request.CompanyName,
            request.TaxCode,
            request.ContactEmail,
            request.PhoneNumber,
            request.Address,
            request.RepresentativeName,
            request.Username,
            request.Password);

        var result = await _registrationService.SubmitAsync(dto, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage == "DuplicateTaxCode")
                return BadRequest(new { error = "DuplicateTaxCode", message = "A business with this tax code is already registered or pending approval." });

            return BadRequest(new { error = "ValidationError", message = result.ErrorMessage });
        }

        return Ok(new BusinessRegistrationResponse(
            result.RequestId!.Value,
            result.BrandId!.Value,
            result.Status.ToString()));
    }

    [HttpGet("register/{requestId:guid}/status")]
    [AllowAnonymous]
    public async Task<ActionResult<RegistrationStatusResponse>> GetStatus(Guid requestId, CancellationToken cancellationToken)
    {
        var status = await _registrationService.GetStatusAsync(requestId, cancellationToken);
        if (status == null)
            return NotFound(new { error = "Registration request not found." });

        return Ok(new RegistrationStatusResponse(
            status.Status.ToString(),
            status.SubmittedAt,
            status.ReviewedAt,
            status.ReviewNotes));
    }
}

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
    private readonly ILogger<PublicRegistrationController> _logger;

    public PublicRegistrationController(IRegistrationService registrationService, ILogger<PublicRegistrationController> logger)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<BusinessRegistrationResponse>> Register(
        [FromBody] SubmitBusinessRegistrationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received business registration for {CompanyName}. FirstBrandName='{FirstBrandName}', ManagerUsername='{ManagerUsername}', HasPassword={HasPassword}.",
            request.CompanyName,
            request.FirstBrandName,
            request.ManagerUsername,
            !string.IsNullOrEmpty(request.ManagerPassword));

        var dto = new RegistrationRequestDto(
            request.CompanyName,
            request.TaxCode,
            request.ContactEmail,
            request.PhoneNumber,
            request.Address,
            request.RepresentativeName,
            request.FirstBrandName,
            request.ManagerUsername,
            request.ManagerPassword);

        var result = await _registrationService.SubmitAsync(dto, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage == "DuplicateTaxCode")
                return BadRequest(new { error = "DuplicateTaxCode", message = "A business with this tax code is already registered or pending approval." });

            return BadRequest(new { error = "ValidationError", message = result.ErrorMessage });
        }

        return Ok(new BusinessRegistrationResponse(
            result.RequestId!.Value,
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
            status.ReviewNotes,
            status.HasFirstBrandDeclaration));
    }

    [HttpPost("register/{requestId:guid}/confirm-contract")]
    [AllowAnonymous]
    public async Task<ActionResult> ConfirmContract(Guid requestId, [FromBody] ConfirmContractDto dto, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _registrationService.ConfirmContractAsync(
            requestId, dto.Token, clientIp, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Contract confirmed successfully." });
    }
}

public class ConfirmContractDto
{
    public string Token { get; set; } = string.Empty;
}

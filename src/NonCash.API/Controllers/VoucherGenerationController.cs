using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/plans/{planId:guid}")]
[Authorize]
public class VoucherGenerationController : ControllerBase
{
    private readonly IVoucherGenerationService _generationService;
    private readonly IVoucherCodeService _voucherCodeService;
    private readonly ICurrentUserService _currentUser;

    public VoucherGenerationController(
        IVoucherGenerationService generationService,
        IVoucherCodeService voucherCodeService,
        ICurrentUserService currentUser)
    {
        _generationService = generationService;
        _voucherCodeService = voucherCodeService;
        _currentUser = currentUser;
    }

    [HttpPost("generate")]
    public async Task<ActionResult> Generate(Guid planId, [FromBody] GenerateRequest request, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var result = await _generationService.GenerateBatchAsync(planId, request.Quantity, brandId.Value, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorMessage?.StartsWith("PlanNotApproved") == true)
                return BadRequest(new { error = "PlanNotApproved", message = "Vouchers can only be generated for approved plans." });

            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { generatedCount = result.GeneratedCount });
    }

    [HttpGet("vouchers")]
    public async Task<ActionResult> ListVouchers(Guid planId, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var details = await _generationService.ListByPlanAsync(planId, brandId.Value, cancellationToken);
        var vouchers = details.Select(d => new
        {
            d.Id,
            d.SerialNo,
            UsageStatus = d.UsageStatus.ToString(),
            d.UsedDate,
            // Generate current dynamic code (short-lived)
            VoucherCode = _voucherCodeService.GenerateCode(d.Id, d.VoucherCodeSecret)
        });

        return Ok(vouchers);
    }
}

public record GenerateRequest(int Quantity);

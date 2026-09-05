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
    private readonly IDistributionBatchService _batchService;
    private readonly ICurrentUserService _currentUser;

    public VoucherGenerationController(
        IVoucherGenerationService generationService,
        IVoucherCodeService voucherCodeService,
        IDistributionBatchService batchService,
        ICurrentUserService currentUser)
    {
        _generationService = generationService;
        _voucherCodeService = voucherCodeService;
        _batchService = batchService;
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

        // Ledger rows carry recipient + lifecycle status so the brand manager can trace
        // every voucher of the plan (who received it, via which run, and where it stands).
        var rows = await _batchService.GetPlanVoucherLedgerAsync(planId, brandId.Value, cancellationToken);
        var vouchers = rows.Select(r => new
        {
            r.Id,
            r.SerialNo,
            r.Status,
            r.UsedDate,
            // Generate current dynamic code (short-lived)
            VoucherCode = _voucherCodeService.GenerateCode(r.Id, r.VoucherCodeSecret),
            r.RecipientPhone,
            r.RecipientName,
            r.DistributionMethod,
            r.DistributedAt,
            r.BatchId
        });

        return Ok(vouchers);
    }
}

public record GenerateRequest(int Quantity);

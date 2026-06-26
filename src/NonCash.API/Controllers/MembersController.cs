using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/members")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IVoucherCodeService _voucherCodeService;

    public MembersController(
        IRepository<VoucherPlanDetail> detailRepository,
        IVoucherPlanRepository planRepository,
        IVoucherCodeService voucherCodeService)
    {
        _detailRepository = detailRepository;
        _planRepository = planRepository;
        _voucherCodeService = voucherCodeService;
    }

    private Guid GetCurrentMemberId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    // AC4: Member sees their owned vouchers (My Voucher).
    [HttpGet("{memberId:guid}/vouchers")]
    public async Task<ActionResult<IEnumerable<MemberVoucherResponse>>> GetMyVouchers(Guid memberId, CancellationToken cancellationToken)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        if (memberId != currentMemberId)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden", message = "You can only view your own vouchers." });

        var details = await _detailRepository.FindAsync(d => d.MemberId == currentMemberId, cancellationToken);
        var detailList = details.ToList();
        if (detailList.Count == 0)
            return Ok(Array.Empty<MemberVoucherResponse>());

        // Load distinct plan headers
        var planIds = detailList.Select(d => d.ParentId).Distinct().ToList();
        var plans = new Dictionary<Guid, VoucherPlanHeader>();
        foreach (var pid in planIds)
        {
            var plan = await _planRepository.GetByIdAsync(pid, cancellationToken);
            if (plan != null)
                plans[pid] = plan;
        }

        var result = detailList
            .OrderByDescending(d => d.CreatedAt)
            .Select(d =>
            {
                plans.TryGetValue(d.ParentId, out var plan);
                return new MemberVoucherResponse(
                    d.Id,
                    d.SerialNo,
                    d.UsageStatus.ToString(),
                    d.UsedDate,
                    plan?.FaceValue ?? 0m,
                    plan?.ValueType.ToString() ?? string.Empty,
                    plan?.ExpiryDate,
                    plan?.ImageUrl,
                    _voucherCodeService.GenerateCode(d.Id, d.VoucherCodeSecret));
            })
            .ToList();

        return Ok(result);
    }
}

public record MemberVoucherResponse(
    Guid Id,
    string SerialNo,
    string UsageStatus,
    DateTime? UsedDate,
    decimal FaceValue,
    string ValueType,
    DateTime? ExpiryDate,
    string? ImageUrl,
    string VoucherCode);

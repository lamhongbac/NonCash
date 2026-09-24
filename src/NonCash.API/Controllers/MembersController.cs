using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly IBrandRepository _brandRepository;
    private readonly IOutletRepository _outletRepository;

    public MembersController(
        IRepository<VoucherPlanDetail> detailRepository,
        IVoucherPlanRepository planRepository,
        IVoucherCodeService voucherCodeService,
        IBrandRepository brandRepository,
        IOutletRepository outletRepository)
    {
        _detailRepository = detailRepository;
        _planRepository = planRepository;
        _voucherCodeService = voucherCodeService;
        _brandRepository = brandRepository;
        _outletRepository = outletRepository;
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

        // Resolve brand names and applicable store names per plan (empty Scope.Outlets = whole brand).
        var brandNames = new Dictionary<Guid, string>();
        var planStoreNames = new Dictionary<Guid, List<string>>();
        var brandIds = plans.Values.Select(p => p.BrandId).Distinct().ToList();
        foreach (var brandId in brandIds)
        {
            var brand = await _brandRepository.GetByIdAsync(brandId, cancellationToken);
            if (brand != null)
                brandNames[brandId] = brand.Name;
        }

        foreach (var plan in plans.Values)
        {
            IReadOnlyList<Outlet> outlets;
            if (plan.Scope.Outlets.Count == 0)
                outlets = (await _outletRepository.FindAsync(o => o.BrandId == plan.BrandId, cancellationToken)).ToList();
            else
                outlets = (await _outletRepository.FindAsync(o => plan.Scope.Outlets.Contains(o.Id), cancellationToken)).ToList();

            planStoreNames[plan.Id] = outlets
                .Select(o => o.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var result = detailList
            .OrderByDescending(d => d.CreatedAt)
            .Select(d =>
            {
                plans.TryGetValue(d.ParentId, out var plan);
                var brandName = plan != null && brandNames.TryGetValue(plan.BrandId, out var bn) ? bn : string.Empty;
                var storeNames = plan != null && planStoreNames.TryGetValue(plan.Id, out var sn) ? sn : new List<string>();
                var scopeSummary = plan == null
                    ? string.Empty
                    : plan.Scope.Outlets.Count == 0
                        ? (string.IsNullOrWhiteSpace(brandName) ? "All stores of the brand" : $"All stores of {brandName}")
                        : $"{plan.Scope.Outlets.Count} selected store(s)";
                return new MemberVoucherResponse(
                    d.Id,
                    d.SerialNo,
                    d.UsageStatus.ToString(),
                    d.UsedDate,
                    plan?.FaceValue ?? 0m,
                    plan?.ValueType.ToString() ?? string.Empty,
                    plan?.ExpiryDate,
                    plan?.ImageUrl,
                    null,
                    brandName,
                    scopeSummary,
                    storeNames);
            })
            .ToList();

        return Ok(result);
    }

    // Mint-on-tap: the code is created only when the member taps to reveal it, so a stolen wallet
    // list no longer hands out a batch of live credentials at once. Throttled per client IP.
    [HttpPost("{memberId:guid}/vouchers/{voucherId:guid}/code")]
    [EnableRateLimiting("member-code")]
    public async Task<IActionResult> MintVoucherCode(Guid memberId, Guid voucherId, CancellationToken cancellationToken)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        if (memberId != currentMemberId)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden", message = "You can only reveal codes for your own vouchers." });

        var detail = await _detailRepository.GetByIdAsync(voucherId, cancellationToken);
        if (detail == null || detail.MemberId != currentMemberId)
            return NotFound(new { error = "NotFound", message = "That voucher is not in your wallet." });

        if (detail.UsageStatus != UsageStatus.Pending && detail.UsageStatus != UsageStatus.InUse)
            return Conflict(new
            {
                error = "VoucherNotUsable",
                message = "This voucher has already been used or has expired. Used or expired vouchers cannot show a redemption code — check the voucher status in your wallet."
            });

        var code = _voucherCodeService.GenerateCode(detail.Id, detail.VoucherCodeSecret);
        return Ok(new { code });
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
    string? VoucherCode,
    string BrandName,
    string ScopeSummary,
    IReadOnlyList<string> ApplicableStores);

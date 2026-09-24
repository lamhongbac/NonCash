using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// CR-2026-09-24-36: redemption history report for a brand — every voucher redeemed at its
/// outlets with face value vs amount actually used (amount_used = the bill amount the POS
/// settled against the voucher). Breakage is face value minus amount used and is only
/// meaningful for fixed-value plans. A BrandManager sees only their own brand; an Admin may
/// filter by brand or see all of them.
/// </summary>
[ApiController]
[Route("api/v1/brand/redemptions")]
[Authorize(Roles = "BrandManager,Admin")]
public class BrandRedemptionsController : ControllerBase
{
    private readonly IRedemptionReportRepository _reportRepository;
    private readonly ICurrentUserService _currentUser;

    public BrandRedemptionsController(IRedemptionReportRepository reportRepository, ICurrentUserService currentUser)
    {
        _reportRepository = reportRepository;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<RedemptionReportResponse>> GetReport(
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        Guid? scopeBrandId;
        if (_currentUser.IsInRole("BrandManager"))
        {
            var ownBrandId = _currentUser.GetCurrentBrandId();
            if (ownBrandId == null)
                return Unauthorized(new
                {
                    error = "Invalid user context.",
                    message = "Your account is not linked to a brand, so there is no redemption report to show. Ask an administrator to link your account to a brand."
                });

            scopeBrandId = ownBrandId;
        }
        else
        {
            scopeBrandId = brandId; // Admin: optional filter, null means every brand.
        }

        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 50;

        var result = await _reportRepository.GetReportAsync(scopeBrandId, outletId, from, to, page, pageSize, cancellationToken);

        return Ok(new RedemptionReportResponse(
            result.Items.Select(i => new RedemptionRowDto(
                i.UsageId,
                i.UsageDate,
                i.TransactionId,
                i.BillNumber,
                i.SerialNo,
                i.ValueType,
                i.FaceValue,
                i.AmountUsed,
                i.Breakage,
                i.OutletId,
                i.OutletName,
                i.PosNo,
                i.OperatorId)).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.TotalFaceValue,
            result.TotalAmountUsed,
            result.TotalBreakage));
    }

    /// <summary>CSV export of the same filtered set the on-screen report totals describe, so a
    /// BrandManager can hand accounting one file instead of copying rows out of the table.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? outletId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        Guid? scopeBrandId;
        if (_currentUser.IsInRole("BrandManager"))
        {
            var ownBrandId = _currentUser.GetCurrentBrandId();
            if (ownBrandId == null)
                return Unauthorized(new
                {
                    error = "Invalid user context.",
                    message = "Your account is not linked to a brand, so there is no redemption report to export. Ask an administrator to link your account to a brand."
                });

            scopeBrandId = ownBrandId;
        }
        else
        {
            scopeBrandId = brandId; // Admin: optional filter, null means every brand.
        }

        var rows = await _reportRepository.GetRowsForExportAsync(scopeBrandId, outletId, from, to, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("UsageDate,TransactionId,BillNumber,SerialNo,ValueType,FaceValue,AmountUsed,Breakage,OutletName,PosNo,OperatorId");
        var ic = CultureInfo.InvariantCulture;
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                r.UsageDate.ToString("yyyy-MM-dd HH:mm:ss", ic),
                r.TransactionId,
                r.BillNumber,
                r.SerialNo,
                r.ValueType,
                r.FaceValue.ToString(ic),
                r.AmountUsed.ToString(ic),
                r.Breakage?.ToString(ic) ?? string.Empty,
                r.OutletName,
                r.PosNo,
                r.OperatorId));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"redemption-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>Outlet list for the report's filter dropdown. BrandManager gets their own
    /// brand's outlets; Admin must pass brandId.</summary>
    [HttpGet("outlets")]
    public async Task<ActionResult<IEnumerable<RedemptionOutletOptionDto>>> GetOutlets(
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken = default)
    {
        Guid? scopeBrandId;
        if (_currentUser.IsInRole("BrandManager"))
        {
            var ownBrandId = _currentUser.GetCurrentBrandId();
            if (ownBrandId == null)
                return Unauthorized(new
                {
                    error = "Invalid user context.",
                    message = "Your account is not linked to a brand, so there are no outlets to list. Ask an administrator to link your account to a brand."
                });

            scopeBrandId = ownBrandId;
        }
        else
        {
            scopeBrandId = brandId;
        }

        if (scopeBrandId == null)
            return BadRequest(new
            {
                error = "Validation",
                message = "Pass brandId to list outlets."
            });

        var outlets = await _reportRepository.GetOutletsForBrandAsync(scopeBrandId.Value, cancellationToken);

        return Ok(outlets.Select(o => new RedemptionOutletOptionDto(o.OutletId, o.OutletName, o.OutletCode)));
    }
}

public record RedemptionRowDto(
    Guid UsageId,
    DateTime UsageDate,
    string TransactionId,
    string? BillNumber,
    string SerialNo,
    string ValueType,
    decimal FaceValue,
    decimal AmountUsed,
    decimal? Breakage,
    Guid OutletId,
    string? OutletName,
    string? PosNo,
    string? OperatorId);

public record RedemptionReportResponse(
    IReadOnlyList<RedemptionRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    decimal TotalFaceValue,
    decimal TotalAmountUsed,
    decimal TotalBreakage);

public record RedemptionOutletOptionDto(Guid OutletId, string OutletName, string? OutletCode);

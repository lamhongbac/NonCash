using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/reports/distribution")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IDistributionReportService _reportService;
    private readonly ICurrentUserService _currentUser;

    // NFR3: Read-only roles allowed to view distribution reports
    private static readonly string[] AllowedRoles = { "BrandManager", "Planner", "Approver", "Admin" };

    public ReportsController(IDistributionReportService reportService, ICurrentUserService currentUser)
    {
        _reportService = reportService;
        _currentUser = currentUser;
    }

    // AC1, AC2, AC5
    [HttpGet]
    public async Task<ActionResult<DistributionSummary>> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!IsRoleAllowed())
            return Forbid();

        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var summary = await _reportService.GetSummaryAsync(brandId.Value, from, to, cancellationToken);
        return Ok(summary);
    }

    // AC3
    [HttpGet("{planId:guid}/details")]
    public async Task<ActionResult<IEnumerable<DistributionDetailItem>>> GetPlanDetails(
        Guid planId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!IsRoleAllowed())
            return Forbid();

        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var details = await _reportService.GetPlanDetailsAsync(brandId.Value, planId, from, to, cancellationToken);
        return Ok(details);
    }

    // AC4: CSV export of summary rows
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!IsRoleAllowed())
            return Forbid();

        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var summary = await _reportService.GetSummaryAsync(brandId.Value, from, to, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("PlanId,PlanDate,VoucherType,FaceValue,Target,Actual,Percentage,Sale,Promotion,Transfer,ExpiryDate,AtRisk");
        var ic = CultureInfo.InvariantCulture;
        foreach (var row in summary.Plans)
        {
            sb.AppendLine(string.Join(",",
                row.PlanId,
                row.PlanDate.ToString("yyyy-MM-dd", ic),
                row.VoucherType,
                row.FaceValue.ToString(ic),
                row.TargetDistributed,
                row.ActualDistributed,
                row.Percentage.ToString(ic),
                row.ByMethod.Sale,
                row.ByMethod.Promotion,
                row.ByMethod.Transfer,
                row.ExpiryDate.ToString("yyyy-MM-dd", ic),
                row.IsAtRisk));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"distribution-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    private bool IsRoleAllowed()
    {
        var role = _currentUser.GetCurrentUserRole();
        return !string.IsNullOrEmpty(role) && AllowedRoles.Contains(role);
    }
}

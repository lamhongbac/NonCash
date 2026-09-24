using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NonCash.API.Controllers;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.Controllers;

/// <summary>
/// CR-2026-09-24-36: brand redemption report. Uses SQLite in-memory with the real
/// RedemptionReportRepository so the join back to the plan (face value), the outlet
/// left-join, the bill-number parse out of TransactionId, and the value-rows-only
/// breakage totals are all exercised against a relational store.
/// </summary>
public class BrandRedemptionsControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;

    private readonly Guid _brandAId = Guid.NewGuid();
    private readonly Guid _brandBId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _outletA1 = Guid.NewGuid();
    private readonly Guid _outletA2 = Guid.NewGuid();
    private readonly Guid _outletB1 = Guid.NewGuid();

    public BrandRedemptionsControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        Seed();
    }

    private void Seed()
    {
        var business = new Business
        {
            BusinessName = "Redemption Test Business",
            TaxCode = "RED-BUS",
            Address = "Test Address",
            IsActive = true
        };
        _context.Businesses.Add(business);
        _context.SaveChanges();

        _context.Brands.AddRange(
            new Brand { Id = _brandAId, BusinessId = business.Id, Name = "Brand A", TaxCode = "RED-A", Status = BrandStatus.Active },
            new Brand { Id = _brandBId, BusinessId = business.Id, Name = "Brand B", TaxCode = "RED-B", Status = BrandStatus.Active });

        _context.UserAccounts.Add(new UserAccount
        {
            Id = _managerId,
            BrandId = _brandAId,
            Username = "redemption.manager",
            PasswordHash = "hash",
            FullName = "Redemption Manager",
            Role = UserRole.BrandManager,
            Status = UserStatus.Active
        });

        _context.Outlets.AddRange(
            new Outlet { Id = _outletA1, BrandId = _brandAId, Name = "A1 Store", Code = "A1", Status = OutletStatus.Active },
            new Outlet { Id = _outletA2, BrandId = _brandAId, Name = "A2 Store", Code = "A2", Status = OutletStatus.Active },
            new Outlet { Id = _outletB1, BrandId = _brandBId, Name = "B1 Store", Code = "B1", Status = OutletStatus.Active });

        var planA = SeedPlan(_brandAId, VoucherValueType.Value, 100000m);
        var planPct = SeedPlan(_brandAId, VoucherValueType.Percentage, 15m);
        var planB = SeedPlan(_brandBId, VoucherValueType.Value, 50000m);

        var dA1 = SeedDetail(planA, "VC-RED-A-00000001");
        var dA2 = SeedDetail(planA, "VC-RED-A-00000002");
        var dA3 = SeedDetail(planA, "VC-RED-A-00000003");
        var dPct = SeedDetail(planPct, "VC-RED-P-00000001");
        var dB1 = SeedDetail(planB, "VC-RED-B-00000001");

        // Midday timestamps so day-boundary filters (from/to are whole days) stay deterministic.
        SeedUsage(dA1, _outletA1, 60000m, "A1-BILL01-638000000000000000", DateTime.UtcNow.Date.AddDays(-2).AddHours(12), posNo: "POS-01", operatorId: "OP-01");
        SeedUsage(dA2, _outletA2, 100000m, "A2-BILL-7-638000000000000001", DateTime.UtcNow);
        SeedUsage(dB1, _outletB1, 40000m, "B1-B99-638000000000000002", DateTime.UtcNow.Date.AddDays(-3).AddHours(12));
        SeedUsage(dPct, _outletA1, 20000m, "A1-BILL02-638000000000000003", DateTime.UtcNow.Date.AddDays(-1).AddHours(12));
        SeedUsage(dA3, _outletA1, 30000m, "MANUAL-123", DateTime.UtcNow.Date.AddDays(-4).AddHours(12));
    }

    private VoucherPlanHeader SeedPlan(Guid brandId, VoucherValueType valueType, decimal faceValue)
    {
        var plan = new VoucherPlanHeader
        {
            PlanDate = DateTime.UtcNow,
            CreatorId = _managerId,
            BrandId = brandId,
            VoucherType = VoucherType.Complimentary,
            ValueType = valueType,
            FaceValue = faceValue,
            NetValue = faceValue,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            PublishDate = DateTime.UtcNow.AddDays(-1),
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddYears(1),
            TargetQuantity = 10,
            Budget = faceValue * 10,
            ApprovalStatus = ApprovalStatus.Approved,
            VersionNumber = 1
        };
        _context.VoucherPlanHeaders.Add(plan);
        _context.SaveChanges();
        return plan;
    }

    private VoucherPlanDetail SeedDetail(VoucherPlanHeader plan, string serialNo)
    {
        var detail = new VoucherPlanDetail
        {
            ParentId = plan.Id,
            SerialNo = serialNo,
            VoucherCodeSecret = $"secret-{serialNo}",
            UsageStatus = UsageStatus.Complete,
            UsedDate = DateTime.UtcNow
        };
        _context.VoucherPlanDetails.Add(detail);
        _context.SaveChanges();
        return detail;
    }

    private void SeedUsage(VoucherPlanDetail detail, Guid outletId, decimal amountUsed, string transactionId,
        DateTime usageDate, string? posNo = null, string? operatorId = null)
    {
        _context.VoucherUsages.Add(new VoucherUsage
        {
            VoucherId = detail.Id,
            PosId = outletId,
            TransactionId = transactionId,
            UsageDate = usageDate,
            AmountUsed = amountUsed,
            RedeemBrandId = _context.Outlets.Find(outletId)!.BrandId,
            PosNo = posNo,
            OperatorId = operatorId
        });
        _context.SaveChanges();
    }

    private BrandRedemptionsController CreateController(string role, Guid? brandId)
    {
        return new BrandRedemptionsController(new RedemptionReportRepository(_context), new TestCurrentUserService(role, brandId));
    }

    // ----- scoping -----

    [Fact]
    public async Task GetReport_BrandManager_ReturnsOnlyOwnBrandRows_WithTotalsAcrossTheFilteredSet()
    {
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetReport(null, null, null, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;

        // u1, u2, u4 (percentage), u5 — brand B's u3 is excluded.
        response.TotalCount.Should().Be(4);
        response.Items.Should().HaveCount(4);

        // Value-plan rows only for face-value aggregates: 100000 x 3 (u1, u2, u5).
        response.TotalFaceValue.Should().Be(300000m);
        response.TotalAmountUsed.Should().Be(210000m); // 60000 + 100000 + 20000 + 30000
        response.TotalBreakage.Should().Be(110000m);   // 40000 + 0 + 70000 (percentage row excluded)
    }

    [Fact]
    public async Task GetReport_AdminWithoutBrandId_SeesAllBrands()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.GetReport(null, null, null, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;
        response.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetReport_AdminFilteringByBrand_SeesOnlyThatBrand()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.GetReport(_brandBId, null, null, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;
        response.TotalCount.Should().Be(1);
        response.Items[0].OutletName.Should().Be("B1 Store");
    }

    [Fact]
    public async Task GetReport_BrandManagerWithoutBrand_ReturnsUnauthorizedWithActionableMessage()
    {
        var controller = CreateController("BrandManager", null);

        var result = await controller.GetReport(null, null, null, null, 1, 50, CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ----- row content -----

    [Fact]
    public async Task GetReport_ParsesBillNumberOutOfTransactionId_WhenShapeHolds()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.GetReport(null, null, null, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;

        // "{StoreCode}-{Bill}-{ticks}" → Bill; dashes inside the bill survive.
        response.Items.Single(i => i.TransactionId == "A2-BILL-7-638000000000000001").BillNumber.Should().Be("BILL-7");
        response.Items.Single(i => i.TransactionId == "A1-BILL01-638000000000000000").BillNumber.Should().Be("BILL01");
    }

    [Fact]
    public async Task GetReport_LeavesBillNumberNull_WhenTransactionIdIsNotStoreBillTicks()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.GetReport(null, null, null, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;
        response.Items.Single(i => i.TransactionId == "MANUAL-123").BillNumber.Should().BeNull();
    }

    [Fact]
    public async Task GetReport_PercentageRow_HasNullBreakage_AndCarriesPosOperatorAttribution()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.GetReport(null, null, null, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;

        var pctRow = response.Items.Single(i => i.SerialNo == "VC-RED-P-00000001");
        pctRow.Breakage.Should().BeNull();
        pctRow.ValueType.Should().Be("Percentage");

        var attributed = response.Items.Single(i => i.TransactionId == "A1-BILL01-638000000000000000");
        attributed.PosNo.Should().Be("POS-01");
        attributed.OperatorId.Should().Be("OP-01");
    }

    [Fact]
    public async Task GetReport_OrdersNewestFirst()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.GetReport(null, null, null, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;
        response.Items.Should().BeInDescendingOrder(i => i.UsageDate);
    }

    // ----- filters -----

    [Fact]
    public async Task GetReport_OutletFilter_NarrowsRowsAndTotals()
    {
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetReport(null, _outletA1, null, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;

        response.TotalCount.Should().Be(3); // u1, u4, u5
        response.TotalAmountUsed.Should().Be(110000m);
        response.Items.Should().OnlyContain(i => i.OutletId == _outletA1);
    }

    [Fact]
    public async Task GetReport_DateFilterFrom_IsInclusiveOfWholeDays()
    {
        var controller = CreateController("BrandManager", _brandAId);
        var from = DateTime.UtcNow.Date.AddDays(-1);

        var result = await controller.GetReport(null, null, from, null, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;

        // u2 (today) and u4 (yesterday) survive; u1 (2 days ago) and u5 (4 days ago) drop out.
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetReport_DateFilterTo_IncludesTheWholeToDay()
    {
        var controller = CreateController("BrandManager", _brandAId);
        var to = DateTime.UtcNow.Date.AddDays(-2);

        var result = await controller.GetReport(null, null, null, to, 1, 50, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;

        // Everything up to end of "2 days ago": u1 (2 days ago) and u5 (4 days ago).
        // u2 (today) and u4 (yesterday) fall after the window.
        response.TotalCount.Should().Be(2);
        response.Items.Select(i => i.TransactionId).Should().BeEquivalentTo(
            new[] { "A1-BILL01-638000000000000000", "MANUAL-123" });
    }

    // ----- outlet options -----

    [Fact]
    public async Task GetOutlets_BrandManager_ReturnsOwnBrandOutlets()
    {
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetOutlets(null, CancellationToken.None);

        var options = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IEnumerable<RedemptionOutletOptionDto>>().Subject
            .ToList();
        options.Should().HaveCount(2);
        options.Select(o => o.OutletId).Should().BeEquivalentTo(new[] { _outletA1, _outletA2 });
    }

    [Fact]
    public async Task GetOutlets_AdminWithoutBrandId_ReturnsBadRequest()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.GetOutlets(null, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ----- CSV export -----

    [Fact]
    public async Task ExportCsv_BrandManager_DownloadsEveryOwnRow_WithHeaderAndAttributionColumns()
    {
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.ExportCsv(null, null, null, null, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileDownloadName.Should().StartWith("redemption-report-").And.EndWith(".csv");

        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        lines[0].Should().Be("UsageDate,TransactionId,BillNumber,SerialNo,ValueType,FaceValue,AmountUsed,Breakage,OutletName,PosNo,OperatorId");
        lines.Should().HaveCount(5); // header + 4 brand-A rows; brand B never leaks.

        csv.Should().Contain("VC-RED-A-00000001");
        csv.Should().Contain("BILL01");                       // parsed out of TransactionId
        csv.Should().Contain("POS-01,OP-01");                 // POS / operator attribution
        csv.Should().NotContain("VC-RED-B-00000001");
    }

    [Fact]
    public async Task ExportCsv_AdminWithBrandFilter_ContainsOnlyThatBrand()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.ExportCsv(_brandBId, null, null, null, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);

        csv.Should().Contain("VC-RED-B-00000001");
        csv.Should().NotContain("VC-RED-A-");
    }

    [Fact]
    public async Task ExportCsv_SameFiltersAsReport_ExportRowCountMatchesReportTotals()
    {
        var controller = CreateController("BrandManager", _brandAId);
        var from = DateTime.UtcNow.Date.AddDays(-1);

        var report = await controller.GetReport(null, null, from, null, 1, 50, CancellationToken.None);
        var totals = report.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RedemptionReportResponse>().Subject;

        var export = await controller.ExportCsv(null, null, from, null, CancellationToken.None);
        var file = export.Should().BeOfType<FileContentResult>().Subject;

        var lines = System.Text.Encoding.UTF8.GetString(file.FileContents)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Should().HaveCount(totals.TotalCount + 1); // + header
    }

    [Fact]
    public async Task ExportCsv_BrandManagerWithoutBrand_ReturnsUnauthorized()
    {
        var controller = CreateController("BrandManager", null);

        var result = await controller.ExportCsv(null, null, null, null, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

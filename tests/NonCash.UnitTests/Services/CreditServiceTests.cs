using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Services;

namespace NonCash.UnitTests.Services;

public class CreditServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly CreditService _sut;
    private readonly Guid _brandId = Guid.NewGuid();

    public CreditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _sut = new CreditService(_context, NullLogger<CreditService>.Instance);
    }

    [Fact]
    public async Task GetBalanceAsync_SumsSignedAmounts()
    {
        // Arrange: +500 grant, +100 purchase, -1 consumption, -49 adjustment
        await _sut.TopUpAsync(_brandId, 500, CreditEntryType.Grant, "welcome", null);
        await _sut.TopUpAsync(_brandId, 100, CreditEntryType.Purchase, "bank ref", null);
        await _sut.TryConsumeAsync(_brandId, Guid.NewGuid());
        await _sut.TopUpAsync(_brandId, -49, CreditEntryType.Adjustment, "correction", null);

        // Act
        var balance = await _sut.GetBalanceAsync(_brandId);

        // Assert
        balance.Should().Be(550);
    }

    [Fact]
    public async Task GetBalanceAsync_WithNoEntries_ReturnsZero()
    {
        var balance = await _sut.GetBalanceAsync(_brandId);

        balance.Should().Be(0);
    }

    [Fact]
    public async Task GetBalanceAsync_IsScopedPerBrand()
    {
        var otherBrandId = Guid.NewGuid();
        await _sut.TopUpAsync(_brandId, 10, CreditEntryType.Grant, null, null);
        await _sut.TopUpAsync(otherBrandId, 99, CreditEntryType.Grant, null, null);

        var balance = await _sut.GetBalanceAsync(_brandId);

        balance.Should().Be(10);
    }

    [Fact]
    public async Task HasCreditAsync_TrueWhenPositive()
    {
        await _sut.TopUpAsync(_brandId, 1, CreditEntryType.Grant, null, null);

        (await _sut.HasCreditAsync(_brandId)).Should().BeTrue();
    }

    [Fact]
    public async Task HasCreditAsync_FalseWhenZero()
    {
        (await _sut.HasCreditAsync(_brandId)).Should().BeFalse();
    }

    [Fact]
    public async Task HasCreditAsync_FalseWhenNegative()
    {
        await _sut.TryConsumeAsync(_brandId, Guid.NewGuid());

        (await _sut.HasCreditAsync(_brandId)).Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeAsync_InsertsConsumptionEntryOfMinusOne()
    {
        var voucherId = Guid.NewGuid();

        await _sut.TryConsumeAsync(_brandId, voucherId, "Sale order 123");

        var entry = await _context.CreditLedgerEntries.SingleAsync();
        entry.BrandId.Should().Be(_brandId);
        entry.EntryType.Should().Be(CreditEntryType.Consumption);
        entry.Amount.Should().Be(-1);
        entry.VoucherDetailId.Should().Be(voucherId);
        entry.Reference.Should().Be("Sale order 123");
    }

    [Fact]
    public async Task TryConsumeAsync_DuplicateVoucher_IsIdempotent()
    {
        var voucherId = Guid.NewGuid();

        await _sut.TryConsumeAsync(_brandId, voucherId);
        await _sut.TryConsumeAsync(_brandId, voucherId);

        // 1 voucher = max 1 credit, ever
        (await _context.CreditLedgerEntries.CountAsync()).Should().Be(1);
        (await _sut.GetBalanceAsync(_brandId)).Should().Be(-1);
    }

    [Fact]
    public async Task TryConsumeAsync_AllowsNegativeBalance()
    {
        // Zero balance — grace overdraft: consumption must still succeed silently
        var act = () => _sut.TryConsumeAsync(_brandId, Guid.NewGuid());

        await act.Should().NotThrowAsync();
        (await _sut.GetBalanceAsync(_brandId)).Should().Be(-1);
    }

    [Fact]
    public async Task TopUpAsync_WithConsumptionType_Throws()
    {
        var act = () => _sut.TopUpAsync(_brandId, -1, CreditEntryType.Consumption, null, null);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("type");
    }

    [Fact]
    public async Task TopUpAsync_WithZeroAmount_Throws()
    {
        var act = () => _sut.TopUpAsync(_brandId, 0, CreditEntryType.Purchase, null, null);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public async Task TopUpAsync_NegativeAmount_RejectedForGrantAndPurchase()
    {
        var actGrant = () => _sut.TopUpAsync(_brandId, -5, CreditEntryType.Grant, null, null);
        var actPurchase = () => _sut.TopUpAsync(_brandId, -5, CreditEntryType.Purchase, null, null);

        await actGrant.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
        await actPurchase.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public async Task TopUpAsync_NegativeAdjustment_IsAllowed()
    {
        await _sut.TopUpAsync(_brandId, 100, CreditEntryType.Grant, null, null);

        var entry = await _sut.TopUpAsync(_brandId, -30, CreditEntryType.Adjustment, "clawback", null);

        entry.Amount.Should().Be(-30);
        (await _sut.GetBalanceAsync(_brandId)).Should().Be(70);
    }

    [Fact]
    public async Task TopUpAsync_RecordsCreatedBy()
    {
        var adminId = Guid.NewGuid();

        var entry = await _sut.TopUpAsync(_brandId, 200, CreditEntryType.Purchase, "bank transfer #42", adminId);

        entry.CreatedBy.Should().Be(adminId);
        entry.Reference.Should().Be("bank transfer #42");
    }

    [Fact]
    public async Task GetLedgerAsync_FiltersByBrandAndType_AndPaginates()
    {
        var otherBrandId = Guid.NewGuid();
        // GetLedgerAsync includes the Brand navigation — seed brand rows for the join.
        _context.Brands.AddRange(
            new Brand { Id = _brandId, Name = "Brand A", TaxCode = "TAX-A" },
            new Brand { Id = otherBrandId, Name = "Brand B", TaxCode = "TAX-B" });
        await _context.SaveChangesAsync();
        await _sut.TopUpAsync(_brandId, 500, CreditEntryType.Grant, null, null);
        await _sut.TopUpAsync(_brandId, 100, CreditEntryType.Purchase, null, null);
        await _sut.TryConsumeAsync(_brandId, Guid.NewGuid());
        await _sut.TopUpAsync(otherBrandId, 500, CreditEntryType.Grant, null, null);

        var all = await _sut.GetLedgerAsync(new CreditLedgerFilters { BrandId = _brandId });
        var consumptionsOnly = await _sut.GetLedgerAsync(new CreditLedgerFilters
        {
            BrandId = _brandId,
            EntryType = CreditEntryType.Consumption
        });
        var pageOne = await _sut.GetLedgerAsync(new CreditLedgerFilters
        {
            BrandId = _brandId,
            Page = 1,
            PageSize = 2
        });

        all.TotalCount.Should().Be(3);
        consumptionsOnly.TotalCount.Should().Be(1);
        consumptionsOnly.Entries.Single().Amount.Should().Be(-1);
        pageOne.Entries.Should().HaveCount(2);
        pageOne.TotalCount.Should().Be(3);
    }
}

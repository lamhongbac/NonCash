using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.UnitTests.Services;

/// <summary>
/// CR-18 StoreStaff CRUD: brand-scope enforcement, validation, and outlet assignment.
/// </summary>
public class StoreStaffServiceTests
{
    private readonly IUserAccountRepository _userRepo = Substitute.For<IUserAccountRepository>();
    private readonly IUserOutletRepository _userOutletRepo = Substitute.For<IUserOutletRepository>();
    private readonly IOutletRepository _outletRepo = Substitute.For<IOutletRepository>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly StoreStaffService _sut;

    private static readonly Guid BrandId = Guid.NewGuid();
    private static readonly Guid OutletId = Guid.NewGuid();

    public StoreStaffServiceTests()
    {
        _authService.HashPassword(Arg.Any<string>()).Returns("hashed");
        _sut = new StoreStaffService(_userRepo, _userOutletRepo, _outletRepo, _authService);
    }

    private Outlet BrandOutlet(Guid? id = null) => new()
    {
        Id = id ?? OutletId,
        BrandId = BrandId,
        Name = "Main Store",
        Status = OutletStatus.Active
    };

    // ── CreateAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Success_CreatesUserAndAssignsOutlets()
    {
        var outlet = BrandOutlet();
        _outletRepo.ListByBrandAsync(BrandId, Arg.Any<CancellationToken>()).Returns(new[] { outlet });
        _userRepo.UsernameExistsAsync("staff1", Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.CreateAsync(BrandId, "staff1", "Password@123", "John Doe", "john@x.com", new[] { outlet.Id });

        result.Username.Should().Be("staff1");
        result.Role.Should().Be(UserRole.StoreStaff);
        result.BrandId.Should().Be(BrandId);
        result.Status.Should().Be(UserStatus.Active);
        await _userRepo.Received(1).AddAsync(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>());
        await _userOutletRepo.Received(1).ReplaceAssignmentsAsync(
            Arg.Any<Guid>(), BrandId, Arg.Is<List<Guid>>(l => l.Contains(outlet.Id)), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task CreateAsync_EmptyUsername_Throws(string? username)
    {
        var act = () => _sut.CreateAsync(BrandId, username!, "Password@123", "Name", null, new[] { OutletId });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Username*");
    }

    [Fact]
    public async Task CreateAsync_ShortPassword_Throws()
    {
        var act = () => _sut.CreateAsync(BrandId, "staff1", "short", "Name", null, new[] { OutletId });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Password*8*");
    }

    [Fact]
    public async Task CreateAsync_NoOutlets_Throws()
    {
        var act = () => _sut.CreateAsync(BrandId, "staff1", "Password@123", "Name", null, Enumerable.Empty<Guid>());
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*outlet*");
    }

    [Fact]
    public async Task CreateAsync_DuplicateUsername_Throws()
    {
        _userRepo.UsernameExistsAsync("staff1", Arg.Any<CancellationToken>()).Returns(true);
        var act = () => _sut.CreateAsync(BrandId, "staff1", "Password@123", "Name", null, new[] { OutletId });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_OutletNotInBrand_Throws()
    {
        var foreignOutlet = BrandOutlet(Guid.NewGuid());
        _outletRepo.ListByBrandAsync(BrandId, Arg.Any<CancellationToken>()).Returns(Array.Empty<Outlet>());

        var act = () => _sut.CreateAsync(BrandId, "staff1", "Password@123", "Name", null, new[] { foreignOutlet.Id });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not in your brand*");
    }

    // ── UpdateAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Success_UpdatesFields()
    {
        var user = StaffUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.UpdateAsync(BrandId, user.Id, "Jane Doe", "jane@x.com", null, null);

        result.FullName.Should().Be("Jane Doe");
        result.Email.Should().Be("jane@x.com");
        await _userRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithPassword_HashesAndSets()
    {
        var user = StaffUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await _sut.UpdateAsync(BrandId, user.Id, "Name", null, "NewPass@123", null);

        _authService.Received(1).HashPassword("NewPass@123");
    }

    [Fact]
    public async Task UpdateAsync_WrongBrand_ThrowsNotFound()
    {
        var user = StaffUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var otherBrand = Guid.NewGuid();

        var act = () => _sut.UpdateAsync(otherBrand, user.Id, "Name", null, null, null);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_NotStoreStaff_ThrowsNotFound()
    {
        var admin = StaffUser();
        admin.Role = UserRole.Admin;
        _userRepo.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        var act = () => _sut.UpdateAsync(BrandId, admin.Id, "Name", null, null, null);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WithOutletReassignment_ReplacesAssignments()
    {
        var user = StaffUser();
        var outlet = BrandOutlet();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _outletRepo.ListByBrandAsync(BrandId, Arg.Any<CancellationToken>()).Returns(new[] { outlet });

        await _sut.UpdateAsync(BrandId, user.Id, "Name", null, null, new[] { outlet.Id });

        await _userOutletRepo.Received(1).ReplaceAssignmentsAsync(
            user.Id, BrandId, Arg.Is<List<Guid>>(l => l.Contains(outlet.Id)), Arg.Any<CancellationToken>());
    }

    // ── LockAsync / UnlockAsync ──────────────────────────────────────

    [Fact]
    public async Task LockAsync_SetsStatusToLocked()
    {
        var user = StaffUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LockAsync(BrandId, user.Id);

        result.Status.Should().Be(UserStatus.Locked);
        await _userRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnlockAsync_SetsStatusToActive()
    {
        var user = StaffUser();
        user.Status = UserStatus.Locked;
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.UnlockAsync(BrandId, user.Id);

        result.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task LockAsync_WrongBrand_ThrowsNotFound()
    {
        var user = StaffUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var act = () => _sut.LockAsync(Guid.NewGuid(), user.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeleteAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingStaff_Deletes()
    {
        var user = StaffUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await _sut.DeleteAsync(BrandId, user.Id);

        _userRepo.Received(1).Delete(user);
        await _userRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        _userRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((UserAccount?)null);

        var act = () => _sut.DeleteAsync(BrandId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── ListAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_FiltersByBrandAndRole()
    {
        var staff = new[] { StaffUser() };
        _userRepo.FindAsync(
            Arg.Any<System.Linq.Expressions.Expression<Func<UserAccount, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns(staff);

        var result = await _sut.ListAsync(BrandId);

        result.Should().HaveCount(1);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsUserAndOutlets()
    {
        var user = StaffUser();
        var outlets = new[] { BrandOutlet() };
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _userOutletRepo.GetOutletsForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(outlets);

        var result = await _sut.GetByIdAsync(BrandId, user.Id);

        result.Should().NotBeNull();
        result!.Value.User.Should().Be(user);
        result.Value.Outlets.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_WrongBrand_ReturnsNull()
    {
        var user = StaffUser();
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), user.Id);

        result.Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static UserAccount StaffUser() => new()
    {
        Id = Guid.NewGuid(),
        Username = "staff1",
        PasswordHash = "hashed",
        FullName = "John Doe",
        Role = UserRole.StoreStaff,
        BrandId = BrandId,
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow
    };
}

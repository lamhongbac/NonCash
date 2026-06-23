using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NonCash.API.Services;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;

namespace NonCash.IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture for voucher transfer acceptance tests.
/// Seeds an SQLite in-memory database with a brand, two member users,
/// vouchers, and all required repositories/services.
/// </summary>
public class TransferAcceptanceTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Context { get; }
    public IAuthService AuthService { get; }
    public IJwtTokenService JwtTokenService { get; }
    public IVoucherTransferService TransferService { get; }
    public IVoucherTransferRepository TransferRepository { get; }
    public IRepository<VoucherPlanDetail> VoucherRepository { get; }
    public ICustomerRepository CustomerRepository { get; }
    public IUserAccountRepository UserRepository { get; }

    public Guid BrandId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public Guid AliceUserId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public Guid BobUserId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public Guid AliceCustomerId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public Guid BobCustomerId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public Guid PlanId { get; } = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public Guid AliceVoucherId { get; } = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public Guid BobVoucherId { get; } = Guid.Parse("50000000-0000-0000-0000-000000000002");

    public TransferAcceptanceTestFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new ApplicationDbContext(options);
        Context.Database.EnsureCreated();

        UserRepository = new UserAccountRepository(Context);
        CustomerRepository = new CustomerRepository(Context);
        VoucherRepository = new Repository<VoucherPlanDetail>(Context);
        TransferRepository = new VoucherTransferRepository(Context);

        var jwtConfig = TestJwtConfig.Create();
        JwtTokenService = new JwtTokenService(jwtConfig);
        AuthService = new AuthService(UserRepository, JwtTokenService);

        TransferService = new VoucherTransferService(
            VoucherRepository,
            CustomerRepository,
            UserRepository,
            TransferRepository);

        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        // Brand
        Context.Brands.Add(new Brand
        {
            Id = BrandId,
            Name = "Test Coffee",
            TaxCode = "TAX-TEST",
            Status = BrandStatus.Active
        });

        // Customers (member profiles)
        Context.Customers.AddRange(
            new Customer
            {
                Id = AliceCustomerId,
                PhoneNumber = "0909111111",
                FullName = "Alice Sender",
                Email = "alice@test.com",
                Status = CustomerStatus.Active
            },
            new Customer
            {
                Id = BobCustomerId,
                PhoneNumber = "0909222222",
                FullName = "Bob Receiver",
                Email = "bob@test.com",
                Status = CustomerStatus.Active
            });

        // User accounts linked to customers
        Context.UserAccounts.AddRange(
            new UserAccount
            {
                Id = AliceUserId,
                BrandId = BrandId,
                CustomerId = AliceCustomerId,
                Username = "alice",
                PasswordHash = AuthService.HashPassword("Test@123"),
                FullName = "Alice Sender",
                Role = UserRole.BrandManager,
                Status = UserStatus.Active
            },
            new UserAccount
            {
                Id = BobUserId,
                BrandId = BrandId,
                CustomerId = BobCustomerId,
                Username = "bob",
                PasswordHash = AuthService.HashPassword("Test@123"),
                FullName = "Bob Receiver",
                Role = UserRole.BrandManager,
                Status = UserStatus.Active
            });

        // Approved voucher plan
        Context.VoucherPlanHeaders.Add(new VoucherPlanHeader
        {
            Id = PlanId,
            PlanDate = DateTime.UtcNow,
            CreatorId = AliceUserId,
            BrandId = BrandId,
            VoucherType = VoucherType.Complimentary,
            ValueType = VoucherValueType.Value,
            FaceValue = 100000m,
            NetValue = 100000m,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            PublishDate = DateTime.UtcNow,
            ValidFrom = DateTime.UtcNow,
            ValidTo = DateTime.UtcNow.AddYears(1),
            TargetQuantity = 10,
            Budget = 1000000m,
            ApprovalStatus = ApprovalStatus.Approved,
            VersionNumber = 1
        });

        // Vouchers owned by users (MemberId = UserAccount.Id)
        Context.VoucherPlanDetails.AddRange(
            new VoucherPlanDetail
            {
                Id = AliceVoucherId,
                ParentId = PlanId,
                SerialNo = "VC-TEST-00000001",
                VoucherCodeSecret = "secret-1",
                MemberId = AliceUserId,
                UsageStatus = UsageStatus.Pending
            },
            new VoucherPlanDetail
            {
                Id = BobVoucherId,
                ParentId = PlanId,
                SerialNo = "VC-TEST-00000002",
                VoucherCodeSecret = "secret-2",
                MemberId = BobUserId,
                UsageStatus = UsageStatus.Pending
            });

        await Context.SaveChangesAsync();
    }

    public string GetTokenForUser(Guid userId)
    {
        var user = Context.UserAccounts.Find(userId)!;
        return JwtTokenService.GenerateToken(user);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

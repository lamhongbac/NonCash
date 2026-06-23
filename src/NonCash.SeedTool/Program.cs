using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;
using MemberType = NonCash.Shared.Enums.MemberType;

Console.WriteLine("=== NonCash Test Data Seed Tool ===\n");

// Setup DI
var services = new ServiceCollection();

var connectionString = Environment.GetEnvironmentVariable("NONCASH_CONNECTION_STRING")
    ?? "Host=localhost;Database=noncash;Username=postgres;Password=postgres";

services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
services.AddScoped<IBrandRepository, BrandRepository>();
services.AddScoped<IOutletRepository, OutletRepository>();
services.AddScoped<ICustomerRepository, CustomerRepository>();
services.AddScoped<IUserAccountRepository, UserAccountRepository>();
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<IVoucherCodeService, VoucherCodeService>();
services.AddScoped<IVoucherPlanRepository, VoucherPlanRepository>();
services.AddScoped<IJwtTokenService, StubJwtTokenService>();

var serviceProvider = services.BuildServiceProvider();

using var scope = serviceProvider.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
var voucherCodeService = scope.ServiceProvider.GetRequiredService<IVoucherCodeService>();

Console.WriteLine("Connecting to database...");

// Test connection
await context.Database.CanConnectAsync();
Console.WriteLine("✓ Database connected\n");

// =============================================================================
// 1. Create Brand
// =============================================================================
Console.WriteLine("Creating brand...");
var brandId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
var brand = await context.Brands.FindAsync(brandId);
if (brand == null)
{
    brand = new Brand
    {
        Id = brandId,
        Name = "Test Coffee Shop",
        TaxCode = "TAX-TEST-001",
        ContactEmail = "admin@testcoffee.com",
        Status = BrandStatus.Active
    };
    context.Brands.Add(brand);
    await context.SaveChangesAsync();
    Console.WriteLine("  ✓ Created: Test Coffee Shop");
}
else
{
    Console.WriteLine("  - Brand already exists, skipping");
}

// =============================================================================
// 2. Create Outlet
// =============================================================================
Console.WriteLine("Creating outlet...");
var outletId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
var outlet = await context.Outlets.FindAsync(outletId);
if (outlet == null)
{
    outlet = new Outlet
    {
        Id = outletId,
        BrandId = brandId,
        Name = "Main Street Store",
        Address = "123 Main Street, HCMC",
        Status = OutletStatus.Active,
        ApiKeyPrefix = "TEST"
    };
    context.Outlets.Add(outlet);
    await context.SaveChangesAsync();
    Console.WriteLine("  ✓ Created: Main Street Store");
}
else
{
    Console.WriteLine("  - Outlet already exists, skipping");
}

// =============================================================================
// 3. Create Members (Customers)
// =============================================================================
Console.WriteLine("Creating members...");

var aliceId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
var bobId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
var carolId = Guid.Parse("c0000000-0000-0000-0000-000000000003");

var members = new[]
{
    (aliceId, "0909111111", "Alice Sender", "alice@test.com"),
    (bobId, "0909222222", "Bob Receiver", "bob@test.com"),
    (carolId, "0909333333", "Carol Third", "carol@test.com")
};

foreach (var (id, phone, name, email) in members)
{
    var customer = await context.Customers.FindAsync(id);
    if (customer == null)
    {
        customer = new Customer
        {
            Id = id,
            PhoneNumber = phone,
            FullName = name,
            Email = email,
            Status = CustomerStatus.Active
        };
        context.Customers.Add(customer);
        Console.WriteLine($"  ✓ Created: {name} ({phone})");
    }
    else
    {
        Console.WriteLine($"  - {name} already exists, skipping");
    }
}
await context.SaveChangesAsync();

// =============================================================================
// 4. Create User Accounts for Members
// =============================================================================
Console.WriteLine("Creating user accounts...");
const string testPassword = "Test@123";
var passwordHash = authService.HashPassword(testPassword);

var aliceUserId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
var bobUserId = Guid.Parse("d0000000-0000-0000-0000-000000000002");

var users = new[]
{
    (aliceUserId, "alice", "Alice Sender", aliceId),
    (bobUserId, "bob", "Bob Receiver", bobId)
};

foreach (var (id, username, fullName, customerId) in users)
{
    var user = await context.UserAccounts.FindAsync(id);
    if (user == null)
    {
        user = new UserAccount
        {
            Id = id,
            BrandId = brandId,
            CustomerId = customerId,
            Username = username,
            PasswordHash = passwordHash,
            FullName = fullName,
            Role = UserRole.BrandManager,
            Status = UserStatus.Active
        };
        context.UserAccounts.Add(user);
        Console.WriteLine($"  ✓ Created: {username} (password: {testPassword})");
    }
    else if (user.CustomerId != customerId)
    {
        user.CustomerId = customerId;
        context.UserAccounts.Update(user);
        Console.WriteLine($"  ✓ Updated: {username} linked to customer {customerId}");
    }
    else
    {
        Console.WriteLine($"  - {username} already correct, skipping");
    }
}
await context.SaveChangesAsync();

// =============================================================================
// 5. Create Approved Voucher Plan
// =============================================================================
Console.WriteLine("Creating voucher plan...");
var planId = Guid.Parse("e0000000-0000-0000-0000-000000000001");
var plan = await context.VoucherPlanHeaders.FindAsync(planId);
if (plan == null)
{
    plan = new VoucherPlanHeader
    {
        Id = planId,
        PlanDate = DateTime.UtcNow,
        CreatorId = aliceUserId,
        BrandId = brandId,
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
        TargetDistributed = 0,
        TargetUsed = 0,
        ApprovalStatus = ApprovalStatus.Approved,
        VersionNumber = 1
    };
    context.VoucherPlanHeaders.Add(plan);
    await context.SaveChangesAsync();
    Console.WriteLine("  ✓ Created: Test Voucher Plan (100,000 VND each)");
}
else
{
    Console.WriteLine("  - Plan already exists, skipping");
}

// =============================================================================
// 6. Create Vouchers (distributed to users)
// =============================================================================
Console.WriteLine("Creating vouchers...");

var vouchers = new[]
{
    // MemberId is UserAccount.Id (from JWT)
    (Guid.Parse("f0000000-0000-0000-0000-000000000001"), "VC-TEST-2026-00000001", aliceUserId, "Alice"),
    (Guid.Parse("f0000000-0000-0000-0000-000000000002"), "VC-TEST-2026-00000002", aliceUserId, "Alice"),
    (Guid.Parse("f0000000-0000-0000-0000-000000000003"), "VC-TEST-2026-00000003", bobUserId, "Bob")
};

foreach (var (id, serialNo, memberId, ownerName) in vouchers)
{
    var detail = await context.VoucherPlanDetails.FindAsync(id);
    if (detail == null)
    {
        detail = new VoucherPlanDetail
        {
            Id = id,
            ParentId = planId,
            SerialNo = serialNo,
            VoucherCodeSecret = voucherCodeService.GenerateSecretKey(),
            MemberId = memberId,
            UsageStatus = UsageStatus.Pending
        };
        context.VoucherPlanDetails.Add(detail);
        Console.WriteLine($"  ✓ Created: {serialNo} → {ownerName}");
    }
    else if (detail.MemberId != memberId)
    {
        detail.MemberId = memberId;
        context.VoucherPlanDetails.Update(detail);
        Console.WriteLine($"  ✓ Updated: {serialNo} → {ownerName}");
    }
    else
    {
        Console.WriteLine($"  - {serialNo} already correct, skipping");
    }
}
await context.SaveChangesAsync();

// =============================================================================
// Summary
// =============================================================================
Console.WriteLine("\n=== Seed Complete ===");
Console.WriteLine($"\nBrands:             {await context.Brands.CountAsync()}");
Console.WriteLine($"Outlets:            {await context.Outlets.CountAsync()}");
Console.WriteLine($"Customers:          {await context.Customers.CountAsync()}");
Console.WriteLine($"UserAccounts:       {await context.UserAccounts.CountAsync()}");
Console.WriteLine($"VoucherPlanHeaders: {await context.VoucherPlanHeaders.CountAsync()}");
Console.WriteLine($"VoucherPlanDetails: {await context.VoucherPlanDetails.CountAsync()}");

Console.WriteLine("\n=== Test Credentials ===");
Console.WriteLine("Alice: username=alice, password=Test@123 (has 2 vouchers)");
Console.WriteLine("Bob:   username=bob,   password=Test@123 (has 1 voucher)");
Console.WriteLine("Admin: username=admin, password=Admin@123");

Console.WriteLine("\n=== Voucher Summary ===");
var aliceVouchers = await context.VoucherPlanDetails.CountAsync(v => v.MemberId == aliceUserId);
var bobVouchers = await context.VoucherPlanDetails.CountAsync(v => v.MemberId == bobUserId);
Console.WriteLine($"Alice owns: {aliceVouchers} voucher(s)");
Console.WriteLine($"Bob owns:   {bobVouchers} voucher(s)");

Console.WriteLine("\nDone! You can now test the transfer flow.");

// Stub implementation for seeding (we only need HashPassword from AuthService)
class StubJwtTokenService : IJwtTokenService
{
    public string GenerateToken(UserAccount user) => "stub-token";
    public DateTime GetTokenExpiry() => DateTime.UtcNow.AddHours(8);
}

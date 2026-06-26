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
services.AddScoped<IMemberAccountRepository, MemberAccountRepository>();
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
// 4. Create Member Accounts for Customers
// =============================================================================
Console.WriteLine("Creating member accounts...");
const string testPassword = "Test@123";
var passwordHash = authService.HashPassword(testPassword);

var aliceMemberId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
var bobMemberId = Guid.Parse("d0000000-0000-0000-0000-000000000002");

var memberAccounts = new[]
{
    (aliceMemberId, "alice", "Alice Sender", aliceId),
    (bobMemberId, "bob", "Bob Receiver", bobId)
};

foreach (var (id, username, fullName, customerId) in memberAccounts)
{
    var member = await context.MemberAccounts.FindAsync(id);
    if (member == null)
    {
        member = new MemberAccount
        {
            Id = id,
            CustomerId = customerId,
            Username = username,
            PasswordHash = passwordHash,
            FullName = fullName,
            Status = MemberAccountStatus.Active
        };
        context.MemberAccounts.Add(member);
        Console.WriteLine($"  ✓ Created member account: {username} (password: {testPassword})");
    }
    else
    {
        member.CustomerId = customerId;
        member.Username = username;
        member.FullName = fullName;
        context.MemberAccounts.Update(member);
        Console.WriteLine($"  - {username} already exists, updated link");
    }
}
await context.SaveChangesAsync();

// =============================================================================
// 5. Create a Staff User Account (plan creator)
// =============================================================================
Console.WriteLine("Creating staff user account...");
var staffUserId = Guid.Parse("e0000000-0000-0000-0000-000000000001");
var staffUser = await context.UserAccounts.FindAsync(staffUserId);
if (staffUser == null)
{
    staffUser = new UserAccount
    {
        Id = staffUserId,
        BrandId = brandId,
        Username = "brandmanager",
        PasswordHash = passwordHash,
        FullName = "Brand Manager",
        Role = UserRole.BrandManager,
        Status = UserStatus.Active
    };
    context.UserAccounts.Add(staffUser);
    await context.SaveChangesAsync();
    Console.WriteLine($"  ✓ Created staff user: brandmanager (password: {testPassword})");
}
else
{
    Console.WriteLine("  - Staff user already exists, skipping");
}

// =============================================================================
// 6. Create Approved Voucher Plan
// =============================================================================
Console.WriteLine("Creating voucher plan...");
var planId = Guid.Parse("f0000000-0000-0000-0000-000000000001");
var plan = await context.VoucherPlanHeaders.FindAsync(planId);
if (plan == null)
{
    plan = new VoucherPlanHeader
    {
        Id = planId,
        PlanDate = DateTime.UtcNow,
        CreatorId = staffUserId,
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
// 7. Create Vouchers (distributed to member accounts)
// =============================================================================
Console.WriteLine("Creating vouchers...");

// Compute the next available test serial number so re-runs do not collide
// with serials already in the database.
var serialPrefix = "VC-TEST-2026-";
var existingSerials = await context.VoucherPlanDetails
    .Where(v => v.SerialNo.StartsWith(serialPrefix))
    .Select(v => v.SerialNo)
    .ToListAsync();

int nextSerialNumber = 1;
foreach (var serial in existingSerials)
{
    if (serial.Length > serialPrefix.Length &&
        int.TryParse(serial.Substring(serialPrefix.Length), out var parsedNumber) &&
        parsedNumber >= nextSerialNumber)
    {
        nextSerialNumber = parsedNumber + 1;
    }
}

var vouchers = new[]
{
    // MemberId is MemberAccount.Id (from JWT)
    (Guid.Parse("f1000000-0000-0000-0000-000000000001"), aliceMemberId, "Alice"),
    (Guid.Parse("f1000000-0000-0000-0000-000000000002"), aliceMemberId, "Alice"),
    (Guid.Parse("f1000000-0000-0000-0000-000000000003"), bobMemberId, "Bob")
};

foreach (var (id, memberId, ownerName) in vouchers)
{
    var serialNo = $"{serialPrefix}{nextSerialNumber++.ToString("D8")}";
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
Console.WriteLine($"MemberAccounts:     {await context.MemberAccounts.CountAsync()}");
Console.WriteLine($"VoucherPlanHeaders: {await context.VoucherPlanHeaders.CountAsync()}");
Console.WriteLine($"VoucherPlanDetails: {await context.VoucherPlanDetails.CountAsync()}");

Console.WriteLine("\n=== Test Credentials ===");
Console.WriteLine("Alice member: username=alice, password=Test@123 (has 2 vouchers)");
Console.WriteLine("Bob member:   username=bob,   password=Test@123 (has 1 voucher)");
Console.WriteLine("Staff:        username=brandmanager, password=Test@123");
Console.WriteLine("Admin:        username=admin, password=Admin@123");

Console.WriteLine("\n=== Voucher Summary ===");
var aliceVouchers = await context.VoucherPlanDetails.CountAsync(v => v.MemberId == aliceMemberId);
var bobVouchers = await context.VoucherPlanDetails.CountAsync(v => v.MemberId == bobMemberId);
Console.WriteLine($"Alice owns: {aliceVouchers} voucher(s)");
Console.WriteLine($"Bob owns:   {bobVouchers} voucher(s)");

Console.WriteLine("\nDone! You can now test the transfer flow.");

// Stub implementation for seeding (we only need HashPassword from AuthService)
class StubJwtTokenService : IJwtTokenService
{
    public string GenerateToken(UserAccount user) => "stub-token";
    public string GenerateToken(MemberAccount member) => "stub-token";
    public DateTime GetTokenExpiry() => DateTime.UtcNow.AddHours(8);
}

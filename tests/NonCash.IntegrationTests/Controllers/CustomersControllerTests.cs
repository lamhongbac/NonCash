using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NonCash.API.Controllers;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.Controllers;

public class CustomersControllerTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Builds the controller acting as the given current user. Defaults to an Admin
    /// (no brand scope — the pre-mapping behavior), so legacy tests are unchanged.
    /// </summary>
    private CustomersController CreateController(ApplicationDbContext context, ICurrentUserService? currentUser = null)
    {
        var repository = new CustomerRepository(context);
        var service = new CustomerService(repository, new BrandCustomerRepository(context));
        var importService = new CsvCustomerImportService(service);
        return new CustomersController(service, importService, currentUser ?? new TestCurrentUserService("Admin"));
    }

    private static async Task<CustomerResponse> CreateCustomerVia(
        CustomersController controller, string phone, string name)
    {
        var result = await controller.CreateCustomer(new CreateCustomerRequest(phone, name, null), CancellationToken.None);
        return (result.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as CustomerResponse
            ?? throw new InvalidOperationException("Create failed");
    }

    [Fact]
    public async Task GetCustomers_ReturnsEmptyList()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetCustomers(null, null, 1, 20, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var paged = okResult!.Value as PagedResult<CustomerResponse>;
        paged!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCustomers_Search_MatchesPhonePartiallyOnDigits()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        await controller.CreateCustomer(new CreateCustomerRequest("0901234567", "Phone Partial One", null), CancellationToken.None);
        await controller.CreateCustomer(new CreateCustomerRequest("0919876543", "Phone Partial Two", null), CancellationToken.None);

        // Formatted input is matched on digits only: "012-3" -> "0123", contained in "0901234567" but not "0919876543".
        var result = await controller.GetCustomers("012-3", null, 1, 20, CancellationToken.None);

        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var paged = okResult!.Value as PagedResult<CustomerResponse>;
        paged!.Items.Should().ContainSingle(c => c.PhoneNumber == "0901234567");
    }

    [Fact]
    public async Task GetCustomers_Search_MatchesNameCaseInsensitively()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        await controller.CreateCustomer(new CreateCustomerRequest("0935000001", "Nguyen Van A", null), CancellationToken.None);

        var result = await controller.GetCustomers("NGUYEN van", null, 1, 20, CancellationToken.None);

        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var paged = okResult!.Value as PagedResult<CustomerResponse>;
        paged!.Items.Should().ContainSingle(c => c.FullName == "Nguyen Van A");
    }

    [Fact]
    public async Task GetCustomers_Search_MatchesEmailPartially()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        await controller.CreateCustomer(new CreateCustomerRequest("0935000002", "Email Match", "partial.match@test.com"), CancellationToken.None);

        var result = await controller.GetCustomers("partial.match@", null, 1, 20, CancellationToken.None);

        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var paged = okResult!.Value as PagedResult<CustomerResponse>;
        paged!.Items.Should().ContainSingle(c => c.Email == "partial.match@test.com");
    }

    [Fact]
    public async Task CreateCustomer_WithValidData_ReturnsCreated()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateCustomerRequest("1234567890", "Integration Test", "int@test.com");

        var result = await controller.CreateCustomer(request, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>();
        var created = result.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
        var customer = created!.Value as CustomerResponse;
        customer!.FullName.Should().Be("Integration Test");
        customer.PhoneNumber.Should().Be("1234567890");
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicatePhone_ReturnsConflict()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateCustomerRequest("5556667777", "Customer A", null);
        await controller.CreateCustomer(request, CancellationToken.None);

        var result = await controller.CreateCustomer(request, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>();
    }

    [Fact]
    public async Task GetCustomerById_WithValidId_ReturnsCustomer()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateCustomerRequest("9998887777", "Get By Id", null);
        var createResult = await controller.CreateCustomer(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as CustomerResponse;

        var result = await controller.GetCustomer(created!.Id, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
    }

    [Fact]
    public async Task GetCustomerById_WithInvalidId_ReturnsNotFound()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetCustomer(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    [Fact]
    public async Task UpdateCustomer_WithValidData_ReturnsOk()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateCustomerRequest("1112223333", "Update Customer", null);
        var createResult = await controller.CreateCustomer(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as CustomerResponse;

        var updateRequest = new UpdateCustomerRequest("Updated Name", "updated@test.com");
        var result = await controller.UpdateCustomer(created!.Id, updateRequest, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var customer = okResult!.Value as CustomerResponse;
        customer!.FullName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task BlacklistCustomer_SetsStatusToBlacklisted()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateCustomerRequest("4445556666", "Blacklist Me", null);
        var createResult = await controller.CreateCustomer(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as CustomerResponse;

        var result = await controller.BlacklistCustomer(created!.Id, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var customer = okResult!.Value as CustomerResponse;
        customer!.Status.Should().Be("Blacklisted");
    }

    [Fact]
    public async Task UnblacklistCustomer_SetsStatusToActive()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateCustomerRequest("7778889999", "Unblacklist Me", null);
        var createResult = await controller.CreateCustomer(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as CustomerResponse;
        await controller.BlacklistCustomer(created!.Id, CancellationToken.None);

        var result = await controller.UnblacklistCustomer(created.Id, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var customer = okResult!.Value as CustomerResponse;
        customer!.Status.Should().Be("Active");
    }

    // ----- Customer model redesign: brand-scoped visibility + per-brand block -----

    [Fact]
    public async Task BrandManager_Search_ReturnsOnlyOwnBrandCustomers() // matrix §4-1
    {
        using var context = CreateContext();
        var brandA = Guid.NewGuid();
        var brandB = Guid.NewGuid();
        var managerA = CreateController(context, new TestCurrentUserService("BrandManager", brandA));
        var managerB = CreateController(context, new TestCurrentUserService("BrandManager", brandB));
        await CreateCustomerVia(managerA, "0900000001", "Brand A Customer");
        await CreateCustomerVia(managerB, "0900000002", "Brand B Customer");

        var result = await managerA.GetCustomers(null, null, 1, 20, CancellationToken.None);

        var paged = (result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as PagedResult<CustomerResponse>;
        paged!.Items.Should().ContainSingle().Which.PhoneNumber.Should().Be("0900000001");
        paged.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Admin_Search_SeesAllBrands() // matrix §4-2
    {
        using var context = CreateContext();
        var brandA = Guid.NewGuid();
        var brandB = Guid.NewGuid();
        await CreateCustomerVia(CreateController(context, new TestCurrentUserService("BrandManager", brandA)), "0900000001", "Brand A Customer");
        await CreateCustomerVia(CreateController(context, new TestCurrentUserService("BrandManager", brandB)), "0900000002", "Brand B Customer");

        var result = await CreateController(context).GetCustomers(null, null, 1, 20, CancellationToken.None);

        var paged = (result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as PagedResult<CustomerResponse>;
        paged!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task BrandManager_GetById_OtherBrandCustomer_ReturnsNotFound() // matrix §4-1
    {
        using var context = CreateContext();
        var brandA = Guid.NewGuid();
        var managerB = CreateController(context, new TestCurrentUserService("BrandManager", Guid.NewGuid()));
        var created = await CreateCustomerVia(CreateController(context, new TestCurrentUserService("BrandManager", brandA)), "0900000003", "Brand A Customer");

        var result = await managerB.GetCustomer(created.Id, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    [Fact]
    public async Task BrandManager_Block_Unblock_Customer() // matrix §4-5 + S1
    {
        using var context = CreateContext();
        var brandA = Guid.NewGuid();
        var manager = CreateController(context, new TestCurrentUserService("BrandManager", brandA));
        var created = await CreateCustomerVia(manager, "0900000004", "Blocked Customer");

        var blockResult = await manager.BlockCustomer(created.Id, null, CancellationToken.None);
        blockResult.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        ((blockResult.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as CustomerResponse)!.IsBrandBlocked.Should().BeTrue();

        // The flag is visible in the brand-scoped list too.
        var list = await manager.GetCustomers(null, null, 1, 20, CancellationToken.None);
        var paged = (list.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as PagedResult<CustomerResponse>;
        paged!.Items.Should().ContainSingle().Which.IsBrandBlocked.Should().BeTrue();

        var unblockResult = await manager.UnblockCustomer(created.Id, null, CancellationToken.None);
        unblockResult.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        ((unblockResult.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as CustomerResponse)!.IsBrandBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Block_WithoutMapping_Returns404() // matrix §4-5
    {
        using var context = CreateContext();
        // Customer created by Admin → no mapping to any brand.
        var admin = CreateController(context);
        var created = await CreateCustomerVia(admin, "0900000005", "Unmapped Customer");

        var result = await CreateController(context, new TestCurrentUserService("BrandManager", Guid.NewGuid()))
            .BlockCustomer(created.Id, null, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    [Fact]
    public async Task Block_AsAdmin_WithoutBrandId_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var admin = CreateController(context);
        var created = await CreateCustomerVia(admin, "0900000006", "Admin Target");

        var result = await admin.BlockCustomer(created.Id, null, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
    }

    [Fact]
    public async Task Block_AsAdmin_WithBrandId_BlocksThatBrand()
    {
        using var context = CreateContext();
        var brandA = Guid.NewGuid();
        var admin = CreateController(context);
        var manager = CreateController(context, new TestCurrentUserService("BrandManager", brandA));
        var created = await CreateCustomerVia(manager, "0900000007", "Admin Block Target");

        var result = await admin.BlockCustomer(created.Id, brandA, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        ((result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as CustomerResponse)!.IsBrandBlocked.Should().BeTrue();
    }

    [Fact]
    public void Blacklist_And_Unblacklist_AreAdminOnly() // matrix §4-6
    {
        var blacklist = typeof(CustomersController).GetMethod(nameof(CustomersController.BlacklistCustomer))!;
        var unblacklist = typeof(CustomersController).GetMethod(nameof(CustomersController.UnblacklistCustomer))!;

        blacklist.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("Admin");
        unblacklist.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("Admin");
    }
}

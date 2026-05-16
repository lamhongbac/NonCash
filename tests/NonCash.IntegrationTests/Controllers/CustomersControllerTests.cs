using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NonCash.API.Controllers;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;

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

    private CustomersController CreateController(ApplicationDbContext context)
    {
        var repository = new CustomerRepository(context);
        var service = new CustomerService(repository);
        var importService = new CsvCustomerImportService(service);
        return new CustomersController(service, importService);
    }

    [Fact]
    public async Task GetCustomers_ReturnsEmptyList()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetCustomers(null, null, null, null, 1, 20, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var paged = okResult!.Value as PagedResult<CustomerResponse>;
        paged!.Items.Should().BeEmpty();
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
}

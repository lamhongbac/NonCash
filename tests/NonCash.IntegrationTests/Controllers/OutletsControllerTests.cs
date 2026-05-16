using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NonCash.API.Controllers;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;

namespace NonCash.IntegrationTests.Controllers;

public class OutletsControllerTests
{
    private readonly Guid _brandId = Guid.NewGuid();

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private OutletsController CreateController(ApplicationDbContext context, Guid? brandId = null)
    {
        var currentUser = new FakeCurrentUserService(brandId ?? _brandId);
        var repository = new OutletRepository(context);
        var service = new OutletService(repository, currentUser);
        return new OutletsController(service);
    }

    [Fact]
    public async Task GetOutlets_ReturnsEmptyList()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetOutlets(null, null, 1, 20, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var paged = okResult!.Value as PagedResult<OutletResponse>;
        paged!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOutlet_WithValidData_ReturnsCreated()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateOutletRequest("Integration Test Outlet", "456 Market St");

        var result = await controller.CreateOutlet(request, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>();
        var created = result.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
        var outlet = created!.Value as OutletResponse;
        outlet!.Name.Should().Be("Integration Test Outlet");
        outlet.Address.Should().Be("456 Market St");
        outlet.BrandId.Should().Be(_brandId);
        outlet.Status.Should().Be("Active");
        outlet.ApiKeyPrefix.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateOutlet_WithEmptyName_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateOutletRequest("  ", null);

        var result = await controller.CreateOutlet(request, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetOutletById_WithValidId_ReturnsOutlet()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateOutletRequest("Get By Id Outlet", "789 Oak Ave");
        var createResult = await controller.CreateOutlet(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as OutletResponse;

        var result = await controller.GetOutlet(created!.Id, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
    }

    [Fact]
    public async Task GetOutletById_WithInvalidId_ReturnsNotFound()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetOutlet(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    [Fact]
    public async Task UpdateOutlet_WithValidData_ReturnsOk()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateOutletRequest("Update Outlet", "111 Pine Rd");
        var createResult = await controller.CreateOutlet(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as OutletResponse;

        var updateRequest = new UpdateOutletRequest("Updated Name", "222 Elm St");
        var result = await controller.UpdateOutlet(created!.Id, updateRequest, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var outlet = okResult!.Value as OutletResponse;
        outlet!.Name.Should().Be("Updated Name");
        outlet.Address.Should().Be("222 Elm St");
    }

    [Fact]
    public async Task CloseOutlet_SetsStatusToClosed()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateOutletRequest("Close Outlet", "333 Maple Dr");
        var createResult = await controller.CreateOutlet(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as OutletResponse;

        var result = await controller.CloseOutlet(created!.Id, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var outlet = okResult!.Value as OutletResponse;
        outlet!.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task GetOutlet_FromOtherBrand_ReturnsNotFound()
    {
        // Arrange
        using var context = CreateContext();
        var otherBrandId = Guid.NewGuid();
        var otherController = CreateController(context, otherBrandId);
        var createRequest = new CreateOutletRequest("Other Brand Outlet", "444 Cedar Ln");
        var createResult = await otherController.CreateOutlet(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as OutletResponse;

        // Act - try to access with different brand
        var myController = CreateController(context, _brandId);
        var result = await myController.GetOutlet(created!.Id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        private readonly Guid _brandId;

        public FakeCurrentUserService(Guid brandId)
        {
            _brandId = brandId;
        }

        public Guid? GetCurrentBrandId() => _brandId;
        public string? GetCurrentUserId() => "test-user";
        public string? GetCurrentUserRole() => "Admin";
        public bool IsInRole(string role) => true;
    }
}

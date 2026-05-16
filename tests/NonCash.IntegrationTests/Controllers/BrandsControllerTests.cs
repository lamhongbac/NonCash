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

public class BrandsControllerTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private BrandsController CreateController(ApplicationDbContext context)
    {
        var repository = new BrandRepository(context);
        var service = new BrandService(repository);
        return new BrandsController(service);
    }

    [Fact]
    public async Task GetBrands_ReturnsEmptyList()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetBrands(null, null, 1, 20, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var paged = okResult!.Value as PagedResult<BrandResponse>;
        paged!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateBrand_WithValidData_ReturnsCreated()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateBrandRequest("Integration Test Brand", "INT999", "int@test.com");

        var result = await controller.CreateBrand(request, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>();
        var created = result.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
        var brand = created!.Value as BrandResponse;
        brand!.Name.Should().Be("Integration Test Brand");
        brand.TaxCode.Should().Be("INT999");
    }

    [Fact]
    public async Task CreateBrand_WithDuplicateTaxCode_ReturnsConflict()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var request = new CreateBrandRequest("Brand A", "DUP001", null);
        await controller.CreateBrand(request, CancellationToken.None);

        var result = await controller.CreateBrand(request, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>();
    }

    [Fact]
    public async Task GetBrandById_WithValidId_ReturnsBrand()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateBrandRequest("Get By Id Brand", "GET001", null);
        var createResult = await controller.CreateBrand(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as BrandResponse;

        var result = await controller.GetBrand(created!.Id, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
    }

    [Fact]
    public async Task GetBrandById_WithInvalidId_ReturnsNotFound()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetBrand(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    [Fact]
    public async Task UpdateBrand_WithValidData_ReturnsOk()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        var createRequest = new CreateBrandRequest("Update Brand", "UPD001", null);
        var createResult = await controller.CreateBrand(createRequest, CancellationToken.None);
        var created = (createResult.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as BrandResponse;

        var updateRequest = new UpdateBrandRequest("Updated Name", "updated@test.com", "Suspended");
        var result = await controller.UpdateBrand(created!.Id, updateRequest, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var brand = okResult!.Value as BrandResponse;
        brand!.Name.Should().Be("Updated Name");
        brand.Status.Should().Be("Suspended");
    }
}

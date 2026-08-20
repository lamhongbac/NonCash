using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/businesses")]
[Authorize(Roles = "Admin")]
public class BusinessesController : ControllerBase
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly INotificationService _notificationService;
    private readonly ApplicationDbContext _dbContext;

    public BusinessesController(IBusinessRepository businessRepository, IBrandRepository brandRepository, INotificationService notificationService, ApplicationDbContext dbContext)
    {
        _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
        _brandRepository = brandRepository ?? throw new ArgumentNullException(nameof(brandRepository));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BusinessResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var businesses = await _businessRepository.GetAllAsync(cancellationToken);
        var brands = await _brandRepository.GetAllAsync(cancellationToken);

        var brandCounts = brands.GroupBy(b => b.BusinessId).ToDictionary(g => g.Key, g => g.Count());

        var response = businesses
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => MapToResponse(b, brandCounts.GetValueOrDefault(b.Id)))
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var business = await _businessRepository.GetByIdAsync(id, cancellationToken);
        if (business == null)
            return NotFound();

        var brandCount = await _brandRepository.CountAsync(b => b.BusinessId == id, cancellationToken);
        return Ok(MapToResponse(business, brandCount));
    }

    [HttpPost]
    public async Task<ActionResult<BusinessResponse>> Create(CreateBusinessRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessName))
            return BadRequest(new { error = "Business name is required." });

        if (string.IsNullOrWhiteSpace(request.TaxCode))
            return BadRequest(new { error = "Tax code is required." });

        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest(new { error = "Address is required." });

        if (await _businessRepository.TaxCodeExistsAsync(request.TaxCode.Trim(), cancellationToken))
            return Conflict(new { error = "A business with this tax code already exists." });

        var business = new Business
        {
            BusinessName = request.BusinessName.Trim(),
            TaxCode = request.TaxCode.Trim(),
            Address = request.Address.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            IsActive = true
        };

        await _businessRepository.AddAsync(business, cancellationToken);
        await _businessRepository.SaveChangesAsync(cancellationToken);

        // Notify the business contact that the business has been created and activated.
        if (!string.IsNullOrWhiteSpace(business.ContactEmail))
        {
            try
            {
                await _notificationService.NotifyBrandCreatedAsync(
                    new BrandCreatedNotification(business.ContactEmail, business.BusinessName, business.BusinessName, business.TaxCode),
                    cancellationToken);
            }
            catch
            {
                // Best-effort: notification failure should not block business creation.
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = business.Id }, MapToResponse(business, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BusinessResponse>> Update(Guid id, UpdateBusinessRequest request, CancellationToken cancellationToken)
    {
        var business = await _businessRepository.GetByIdAsync(id, cancellationToken);
        if (business == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.BusinessName))
            return BadRequest(new { error = "Business name is required." });

        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest(new { error = "Address is required." });

        business.BusinessName = request.BusinessName.Trim();
        business.Address = request.Address.Trim();
        business.ContactEmail = request.ContactEmail?.Trim();
        business.PhoneNumber = request.PhoneNumber?.Trim();
        business.IsActive = request.IsActive;

        _businessRepository.Update(business);
        await _businessRepository.SaveChangesAsync(cancellationToken);

        var brandCount = await _brandRepository.CountAsync(b => b.BusinessId == id, cancellationToken);
        return Ok(MapToResponse(business, brandCount));
    }

    /// <summary>
    /// TEMPORARY: Hard-deletes a business and all directly related records. Intended for test cleanup only.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var business = await _businessRepository.GetByIdAsync(id, cancellationToken);
        if (business == null)
            return NotFound();

        var brandIds = await _dbContext.Brands
            .Where(b => b.BusinessId == id)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (brandIds.Any())
            {
                // Brand-scoped records.
                await _dbContext.CreditConsumptions
                    .Where(c => brandIds.Contains(c.BrandId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.CreditLedgerEntries
                    .Where(e => brandIds.Contains(e.BrandId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.CreditBatches
                    .Where(b => brandIds.Contains(b.BrandId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.BusinessRegistrationRequests
                    .Where(r => r.BrandId.HasValue && brandIds.Contains(r.BrandId.Value))
                    .ExecuteDeleteAsync(cancellationToken);

                // Voucher plan headers reference user accounts (creator) and brands (sponsor),
                // so they must be deleted before those tables are touched.
                await _dbContext.VoucherPlanHeaders
                    .Where(v => brandIds.Contains(v.BrandId) || (v.SponsorBrandId.HasValue && brandIds.Contains(v.SponsorBrandId.Value)))
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.Outlets
                    .Where(o => brandIds.Contains(o.BrandId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.UserAccounts
                    .Where(u => u.BrandId.HasValue && brandIds.Contains(u.BrandId.Value))
                    .ExecuteDeleteAsync(cancellationToken);

                // Brands themselves.
                await _dbContext.Brands
                    .Where(b => b.BusinessId == id)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // Registration requests linked directly to the business (with no brand yet).
            await _dbContext.BusinessRegistrationRequests
                .Where(r => r.BusinessId == id)
                .ExecuteDeleteAsync(cancellationToken);

            // Welcome grant policies reference the business directly.
            await _dbContext.WelcomeGrantPolicies
                .Where(p => p.BusinessId == id)
                .ExecuteDeleteAsync(cancellationToken);

            // Business itself.
            await _dbContext.Businesses
                .Where(b => b.Id == id)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Ok(new { message = "Business deleted successfully." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { error = $"Failed to delete business: {ex.Message}" });
        }
    }

    private static BusinessResponse MapToResponse(Business business, int brandCount)
    {
        return new BusinessResponse(
            business.Id,
            business.BusinessName,
            business.TaxCode,
            business.Address,
            business.ContactEmail,
            business.PhoneNumber,
            business.IsActive,
            brandCount,
            business.CreatedAt,
            business.UpdatedAt
        );
    }
}

using NonCash.Core.Configuration;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class BrandService
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly ICreditService? _creditService;
    private readonly CreditConfig _creditConfig;

    public BrandService(
        IBusinessRepository businessRepository,
        IBrandRepository brandRepository,
        ICreditService? creditService = null,
        CreditConfig? creditConfig = null)
    {
        _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
        _brandRepository = brandRepository ?? throw new ArgumentNullException(nameof(brandRepository));
        _creditService = creditService;
        _creditConfig = creditConfig ?? new CreditConfig();
    }

    public async Task<Brand> CreateAsync(Guid businessId, string name, string taxCode, string? contactEmail, CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty)
            throw new ArgumentException("Business is required.", nameof(businessId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(taxCode))
            throw new ArgumentException("TaxCode is required.", nameof(taxCode));

        var business = await _businessRepository.GetByIdAsync(businessId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business with ID '{businessId}' not found.");

        if (await _brandRepository.TaxCodeExistsAsync(taxCode, cancellationToken))
            throw new InvalidOperationException($"A brand with tax code '{taxCode}' already exists.");

        var brand = new Brand
        {
            BusinessId = businessId,
            Name = name.Trim(),
            TaxCode = taxCode.Trim(),
            ContactEmail = contactEmail?.Trim(),
            Status = BrandStatus.Active
        };

        await _brandRepository.AddAsync(brand, cancellationToken);
        await _brandRepository.SaveChangesAsync(cancellationToken);

        // Epic 10: welcome credit grant for each newly activated brand (policy-driven).
        if (_creditService != null)
        {
            await _creditService.GrantWelcomeAsync(brand.Id, cancellationToken: cancellationToken);
        }

        brand.Business = business;
        return brand;
    }

    public async Task<Brand> UpdateAsync(Guid id, string name, string? contactEmail, BrandStatus status, CancellationToken cancellationToken = default)
    {
        var brand = await _brandRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Brand with ID '{id}' not found.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        brand.Name = name.Trim();
        brand.ContactEmail = contactEmail?.Trim();
        brand.Status = status;

        await _brandRepository.SaveChangesAsync(cancellationToken);

        return brand;
    }

    public async Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _brandRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<Brand>> ListAsync(string? nameFilter, BrandStatus? statusFilter, Guid? businessId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var brands = await _brandRepository.GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            brands = brands.Where(b => b.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter.HasValue)
        {
            brands = brands.Where(b => b.Status == statusFilter.Value);
        }

        if (businessId.HasValue && businessId.Value != Guid.Empty)
        {
            brands = brands.Where(b => b.BusinessId == businessId.Value);
        }

        return brands
            .OrderByDescending(b => b.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<int> CountAsync(string? nameFilter, BrandStatus? statusFilter, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var brands = await _brandRepository.GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            brands = brands.Where(b => b.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter.HasValue)
        {
            brands = brands.Where(b => b.Status == statusFilter.Value);
        }

        if (businessId.HasValue && businessId.Value != Guid.Empty)
        {
            brands = brands.Where(b => b.BusinessId == businessId.Value);
        }

        return brands.Count();
    }
}

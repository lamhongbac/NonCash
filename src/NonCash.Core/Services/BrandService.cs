using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class BrandService
{
    private readonly IBrandRepository _brandRepository;

    public BrandService(IBrandRepository brandRepository)
    {
        _brandRepository = brandRepository ?? throw new ArgumentNullException(nameof(brandRepository));
    }

    public async Task<Brand> CreateAsync(string name, string taxCode, string? contactEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(taxCode))
            throw new ArgumentException("TaxCode is required.", nameof(taxCode));

        if (await _brandRepository.TaxCodeExistsAsync(taxCode, cancellationToken))
            throw new InvalidOperationException($"A brand with tax code '{taxCode}' already exists.");

        var brand = new Brand
        {
            Name = name.Trim(),
            TaxCode = taxCode.Trim(),
            ContactEmail = contactEmail?.Trim(),
            Status = BrandStatus.Active
        };

        await _brandRepository.AddAsync(brand, cancellationToken);
        await _brandRepository.SaveChangesAsync(cancellationToken);

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

    public async Task<IEnumerable<Brand>> ListAsync(string? nameFilter, BrandStatus? statusFilter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
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

        return brands
            .OrderByDescending(b => b.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<int> CountAsync(string? nameFilter, BrandStatus? statusFilter, CancellationToken cancellationToken = default)
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

        return brands.Count();
    }
}

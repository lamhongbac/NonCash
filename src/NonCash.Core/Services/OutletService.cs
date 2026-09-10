using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class OutletService
{
    private readonly IOutletRepository _outletRepository;
    private readonly ICurrentUserService _currentUserService;

    public OutletService(IOutletRepository outletRepository, ICurrentUserService currentUserService)
    {
        _outletRepository = outletRepository ?? throw new ArgumentNullException(nameof(outletRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Outlet> CreateAsync(
        string name,
        string? address,
        string? code = null,
        PosRedemptionMode posRedemptionMode = PosRedemptionMode.OneClick,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Outlet name is required.", nameof(name));

        var brandId = _currentUserService.GetCurrentBrandId()
            ?? throw new InvalidOperationException("Current BrandID is not available.");

        var outlet = new Outlet
        {
            BrandId = brandId,
            Name = name.Trim(),
            Address = address?.Trim(),
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim(),
            Status = OutletStatus.Active,
            ApiKeyPrefix = GenerateApiKeyPrefix(),
            PosRedemptionMode = posRedemptionMode
        };

        await _outletRepository.AddAsync(outlet, cancellationToken);
        await _outletRepository.SaveChangesAsync(cancellationToken);
        return outlet;
    }

    public async Task<(IEnumerable<Outlet> Items, int TotalCount)> ListByBrandAsync(
        string? name,
        OutletStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var brandId = _currentUserService.GetCurrentBrandId()
            ?? throw new InvalidOperationException("Current BrandID is not available.");

        var outlets = await _outletRepository.ListByBrandAsync(brandId, cancellationToken);

        var query = outlets.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(o => o.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderBy(o => o.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public async Task<Outlet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var brandId = _currentUserService.GetCurrentBrandId()
            ?? throw new InvalidOperationException("Current BrandID is not available.");

        var outlet = await _outletRepository.GetByIdAsync(id, cancellationToken);
        if (outlet == null || outlet.BrandId != brandId)
            return null;

        return outlet;
    }

    public async Task<Outlet> UpdateAsync(
        Guid id,
        string name,
        string? address,
        string? code = null,
        PosRedemptionMode? posRedemptionMode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Outlet name is required.", nameof(name));

        var brandId = _currentUserService.GetCurrentBrandId()
            ?? throw new InvalidOperationException("Current BrandID is not available.");

        var outlet = await _outletRepository.GetByIdAsync(id, cancellationToken);
        if (outlet == null || outlet.BrandId != brandId)
            throw new KeyNotFoundException($"Outlet with ID '{id}' was not found in your brand.");

        outlet.Name = name.Trim();
        outlet.Address = address?.Trim();
        outlet.Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        if (posRedemptionMode.HasValue)
            outlet.PosRedemptionMode = posRedemptionMode.Value;

        await _outletRepository.SaveChangesAsync(cancellationToken);
        return outlet;
    }

    public async Task<Outlet> CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var brandId = _currentUserService.GetCurrentBrandId()
            ?? throw new InvalidOperationException("Current BrandID is not available.");

        var outlet = await _outletRepository.GetByIdAsync(id, cancellationToken);
        if (outlet == null || outlet.BrandId != brandId)
            throw new KeyNotFoundException($"Outlet with ID '{id}' was not found in your brand.");

        outlet.Status = OutletStatus.Closed;

        await _outletRepository.SaveChangesAsync(cancellationToken);
        return outlet;
    }

    private static string GenerateApiKeyPrefix()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
}

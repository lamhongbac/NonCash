using NonCash.Core.Interfaces;

namespace NonCash.IntegrationTests.Fixtures;

/// <summary>
/// Configurable ICurrentUserService for controller tests: lets a test act as
/// an Admin (no brand scope) or as a BrandManager scoped to a specific brand.
/// </summary>
public class TestCurrentUserService : ICurrentUserService
{
    private readonly Guid? _brandId;
    private readonly string? _role;
    private readonly string? _userId;

    public TestCurrentUserService(string role, Guid? brandId = null, string? userId = null)
    {
        _role = role;
        _brandId = brandId;
        _userId = userId;
    }

    public Guid? GetCurrentBrandId() => _brandId;
    public Guid? GetCurrentCustomerId() => null;
    public string? GetCurrentUserId() => _userId;
    public string? GetCurrentUserRole() => _role;
    public bool IsInRole(string role) => string.Equals(_role, role, StringComparison.OrdinalIgnoreCase);
}

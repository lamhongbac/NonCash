using System.Security.Claims;
using NonCash.Core.Interfaces;

namespace NonCash.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public Guid? GetCurrentBrandId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return null;

        var brandIdClaim = user.FindFirst("brand_id")?.Value
            ?? user.FindFirst(ClaimTypes.GroupSid)?.Value;

        if (Guid.TryParse(brandIdClaim, out var brandId))
        {
            return brandId;
        }

        return null;
    }

    public Guid? GetCurrentCustomerId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return null;

        var customerIdClaim = user.FindFirst("customer_id")?.Value;

        if (Guid.TryParse(customerIdClaim, out var customerId))
        {
            return customerId;
        }

        return null;
    }

    public string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public string? GetCurrentUserRole()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return null;

        return user.FindFirst(ClaimTypes.Role)?.Value;
    }

    public bool IsInRole(string role)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return false;

        return user.IsInRole(role);
    }
}

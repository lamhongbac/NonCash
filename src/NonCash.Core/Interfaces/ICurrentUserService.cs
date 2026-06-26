namespace NonCash.Core.Interfaces;

public interface ICurrentUserService
{
    Guid? GetCurrentBrandId();
    Guid? GetCurrentCustomerId();
    string? GetCurrentUserId();
    string? GetCurrentUserRole();
    bool IsInRole(string role);
}

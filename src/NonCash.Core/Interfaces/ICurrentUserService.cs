namespace NonCash.Core.Interfaces;

public interface ICurrentUserService
{
    Guid? GetCurrentBrandId();
    string? GetCurrentUserId();
    string? GetCurrentUserRole();
    bool IsInRole(string role);
}

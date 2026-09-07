using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

/// <summary>
/// CRUD for StoreStaff users (CR-2026-09-07-18). All operations are brand-scoped —
/// the caller's BrandId is the authoritative boundary (never trusted from the request body).
/// BrandManager cannot create Admin/BrandManager/Planner/Approver/FinancialController users.
/// </summary>
public class StoreStaffService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IUserOutletRepository _userOutletRepository;
    private readonly IOutletRepository _outletRepository;
    private readonly IAuthService _authService;

    public StoreStaffService(
        IUserAccountRepository userRepository,
        IUserOutletRepository userOutletRepository,
        IOutletRepository outletRepository,
        IAuthService authService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _userOutletRepository = userOutletRepository ?? throw new ArgumentNullException(nameof(userOutletRepository));
        _outletRepository = outletRepository ?? throw new ArgumentNullException(nameof(outletRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<UserAccount> CreateAsync(
        Guid callerBrandId,
        string username,
        string password,
        string fullName,
        string? email,
        IEnumerable<Guid> outletIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        var outlets = outletIds.Distinct().ToList();
        if (outlets.Count == 0)
            throw new ArgumentException("At least one outlet assignment is required.", nameof(outletIds));

        if (await _userRepository.UsernameExistsAsync(username, cancellationToken))
            throw new InvalidOperationException($"Username '{username}' already exists.");

        // Verify all outlets belong to the caller's brand (security: do not trust request).
        var brandOutlets = (await _outletRepository.ListByBrandAsync(callerBrandId, cancellationToken)).ToList();
        var invalid = outlets.Except(brandOutlets.Select(o => o.Id)).ToList();
        if (invalid.Count > 0)
            throw new ArgumentException($"Outlet(s) not in your brand: {string.Join(", ", invalid)}");

        var user = new UserAccount
        {
            Username = username.Trim().ToLowerInvariant(),
            PasswordHash = _authService.HashPassword(password),
            FullName = fullName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Role = UserRole.StoreStaff,
            BrandId = callerBrandId,
            Status = UserStatus.Active
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        await _userOutletRepository.ReplaceAssignmentsAsync(user.Id, callerBrandId, outlets, cancellationToken);

        return user;
    }

    public async Task<UserAccount> UpdateAsync(
        Guid callerBrandId,
        Guid staffId,
        string fullName,
        string? email,
        string? password,
        IEnumerable<Guid>? outletIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        var user = await _userRepository.GetByIdAsync(staffId, cancellationToken);
        if (user == null || user.Role != UserRole.StoreStaff || user.BrandId != callerBrandId)
            throw new KeyNotFoundException($"StoreStaff '{staffId}' was not found in your brand.");

        user.FullName = fullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        if (!string.IsNullOrWhiteSpace(password))
        {
            if (password.Length < 8)
                throw new ArgumentException("Password must be at least 8 characters.", nameof(password));
            user.PasswordHash = _authService.HashPassword(password);
        }

        await _userRepository.SaveChangesAsync(cancellationToken);

        if (outletIds != null)
        {
            var outlets = outletIds.Distinct().ToList();
            if (outlets.Count == 0)
                throw new ArgumentException("At least one outlet assignment is required.", nameof(outletIds));

            var brandOutlets = (await _outletRepository.ListByBrandAsync(callerBrandId, cancellationToken)).ToList();
            var invalid = outlets.Except(brandOutlets.Select(o => o.Id)).ToList();
            if (invalid.Count > 0)
                throw new ArgumentException($"Outlet(s) not in your brand: {string.Join(", ", invalid)}");

            await _userOutletRepository.ReplaceAssignmentsAsync(user.Id, callerBrandId, outlets, cancellationToken);
        }

        return user;
    }

    public async Task<UserAccount> LockAsync(Guid callerBrandId, Guid staffId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(staffId, cancellationToken);
        if (user == null || user.Role != UserRole.StoreStaff || user.BrandId != callerBrandId)
            throw new KeyNotFoundException($"StoreStaff '{staffId}' was not found in your brand.");

        user.Status = UserStatus.Locked;
        await _userRepository.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<UserAccount> UnlockAsync(Guid callerBrandId, Guid staffId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(staffId, cancellationToken);
        if (user == null || user.Role != UserRole.StoreStaff || user.BrandId != callerBrandId)
            throw new KeyNotFoundException($"StoreStaff '{staffId}' was not found in your brand.");

        user.Status = UserStatus.Active;
        await _userRepository.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task DeleteAsync(Guid callerBrandId, Guid staffId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(staffId, cancellationToken);
        if (user == null || user.Role != UserRole.StoreStaff || user.BrandId != callerBrandId)
            throw new KeyNotFoundException($"StoreStaff '{staffId}' was not found in your brand.");

        _userRepository.Delete(user);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserAccount>> ListAsync(Guid callerBrandId, CancellationToken cancellationToken = default)
    {
        return await _userRepository.FindAsync(
            u => u.BrandId == callerBrandId && u.Role == UserRole.StoreStaff,
            cancellationToken);
    }

    public async Task<(UserAccount User, IEnumerable<Outlet> Outlets)?> GetByIdAsync(
        Guid callerBrandId, Guid staffId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(staffId, cancellationToken);
        if (user == null || user.Role != UserRole.StoreStaff || user.BrandId != callerBrandId)
            return null;

        var outlets = await _userOutletRepository.GetOutletsForUserAsync(user.Id, cancellationToken);
        return (user, outlets);
    }
}

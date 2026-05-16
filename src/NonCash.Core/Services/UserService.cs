using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class UserService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IAuthService _authService;

    public UserService(IUserAccountRepository userRepository, IAuthService authService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<UserAccount> CreateAsync(string username, string password, string fullName, UserRole role, Guid? brandId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        if (await _userRepository.UsernameExistsAsync(username, cancellationToken))
            throw new InvalidOperationException($"Username '{username}' already exists.");

        if (role != UserRole.Admin && brandId == null)
            throw new ArgumentException("Non-admin users must be assigned to a brand.", nameof(brandId));

        var user = new UserAccount
        {
            Username = username.Trim().ToLowerInvariant(),
            PasswordHash = _authService.HashPassword(password),
            FullName = fullName.Trim(),
            Role = role,
            BrandId = brandId,
            Status = UserStatus.Active
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<UserAccount> LockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        user.Status = UserStatus.Locked;
        await _userRepository.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<UserAccount> UnlockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        user.Status = UserStatus.Active;
        await _userRepository.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<IEnumerable<UserAccount>> ListAsync(Guid? brandId, CancellationToken cancellationToken = default)
    {
        if (brandId.HasValue)
        {
            return await _userRepository.ListByBrandAsync(brandId.Value, cancellationToken);
        }
        return await _userRepository.GetAllAsync(cancellationToken);
    }

    public async Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetByIdAsync(id, cancellationToken);
    }
}

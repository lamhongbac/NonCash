namespace NonCash.API.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid UserId,
    string FullName,
    string Role,
    Guid? BrandId,
    Guid? CustomerId
);

public record CreateUserRequest(
    string Username,
    string Password,
    string FullName,
    string Role,
    Guid? BrandId
);

public record UpdateUserRequest(
    string FullName,
    string Role,
    Guid? BrandId,
    string? Password
);

public record UserResponse(
    Guid Id,
    string Username,
    string FullName,
    string Role,
    Guid? BrandId,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

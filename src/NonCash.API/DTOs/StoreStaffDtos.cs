namespace NonCash.API.DTOs;

public record CreateStoreStaffRequest(
    string Username,
    string Password,
    string FullName,
    string? Email,
    IReadOnlyList<Guid> OutletIds
);

public record UpdateStoreStaffRequest(
    string FullName,
    string? Email,
    string? Password,
    IReadOnlyList<Guid>? OutletIds
);

public record StoreStaffResponse(
    Guid Id,
    string Username,
    string FullName,
    string? Email,
    string Status,
    IReadOnlyList<StoreStaffOutletDto> Outlets,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record StoreStaffOutletDto(
    Guid Id,
    string? Code,
    string Name
);

public record StaffLoginRequest(
    string Username,
    string StoreCode,
    string Password
);

public record StaffLoginResponse(
    string Token,
    DateTime ExpiresAt,
    UserDto User,
    Guid OutletId
);

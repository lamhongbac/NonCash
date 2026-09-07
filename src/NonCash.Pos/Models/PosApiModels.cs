namespace NonCash.Pos.Models;

// ── POS API responses (mirror PosController DTOs) ──────────────────────

public record PosVerifyResponse(string Status, string? Reason, PosVoucherInfo? VoucherInfo);

public record PosVoucherInfo(
    decimal FaceValue,
    string ValueType,
    DateTime ExpiryDate,
    string BrandName,
    string SerialNo
);

public record PosLockResponse(string Status, string? Reason, Guid? LockId, PosVoucherInfo? VoucherInfo);

public record PosCommitResponse(string Status, string? Reason, string? Message);

public record PosRollbackResponse(string Status, string? Reason, string? Message);

// ── POS API requests ───────────────────────────────────────────────────

public record PosVerifyRequest(string VoucherCode, Guid OutletId);

public record PosLockRequest(string VoucherCode, Guid OutletId, string BillNumber, string? PosNo, string? OperatorId);

public record PosCommitRequest(Guid LockId, string TransactionId, decimal AmountUsed, string? PosNo, string? OperatorId);

public record PosRollbackRequest(Guid LockId);

// ── Staff login (app-shell, JWT) ───────────────────────────────────────

public record StaffLoginApiRequest(string Username, string StoreCode, string Password);

public record StaffLoginApiResponse(
    string Token,
    DateTime ExpiresAt,
    StaffLoginUser User,
    Guid OutletId
);

public record StaffLoginUser(
    Guid UserId,
    string FullName,
    string Role,
    Guid? BrandId,
    Guid? CustomerId
);

// ── App state models ───────────────────────────────────────────────────

/// <summary>Cached outlet info from provisioning (stored in localStorage).</summary>
public record ProvisionedOutlet(string ApiKey, Guid OutletId, string StoreCode, string OutletName);

/// <summary>Current shift session (in-memory only, lost on refresh).</summary>
public class ShiftSession
{
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public Guid OutletId { get; set; }
    public string OutletName { get; set; } = string.Empty;
    public string StoreCode { get; set; } = string.Empty;
    public string JwtToken { get; set; } = string.Empty;
    public DateTime ShiftStart { get; set; }
    public DateTime TokenExpiry { get; set; }
    public string? PosNo { get; set; }

    /// <summary>Transactions completed during this shift.</summary>
    public List<ShiftTransaction> Transactions { get; set; } = new();

    public bool IsExpired => DateTime.UtcNow >= TokenExpiry;
    public TimeSpan Remaining => TokenExpiry - DateTime.UtcNow;
}

public class ShiftTransaction
{
    public DateTime Timestamp { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public string VoucherSerialNo { get; set; } = string.Empty;
    public decimal AmountUsed { get; set; }
    public string Status { get; set; } = string.Empty; // Committed, RolledBack
}

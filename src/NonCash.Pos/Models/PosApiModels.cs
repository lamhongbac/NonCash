namespace NonCash.Pos.Models;

// ── POS API responses (mirror PosController DTOs) ──────────────────────

public record PosVerifyResponse(string Status, string? Reason, PosVoucherInfo? VoucherInfo);

public record PosVoucherInfo(
    decimal FaceValue,
    string ValueType,
    DateTime ExpiryDate,
    string BrandName,
    string SerialNo,
    string ScopeSummary,
    List<string> ApplicableOutlets
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
    Guid OutletId,
    string OutletName,
    string RedemptionMode = "OneClick"
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
/// <remarks>
/// OutletId/OutletName are kept only for backward compatibility with previously stored
/// terminals; the authoritative outlet is resolved at staff login (ShiftSession.OutletId).
/// </remarks>
public record ProvisionedOutlet(string ApiKey, string StoreCode, string PosNo = "", Guid OutletId = default, string OutletName = "");

/// <summary>Current shift session (in-memory only, lost on refresh).</summary>
public class ShiftSession
{
    public const string OneClickMode = "OneClick";
    public const string ThreeStepMode = "ThreeStep";

    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public Guid OutletId { get; set; }
    public string OutletName { get; set; } = string.Empty;
    public string StoreCode { get; set; } = string.Empty;
    public string JwtToken { get; set; } = string.Empty;
    public DateTime ShiftStart { get; set; }
    public DateTime TokenExpiry { get; set; }
    public string? PosNo { get; set; }

    /// <summary>Redemption flow configured for this outlet: OneClick (default) or ThreeStep.</summary>
    public string RedemptionMode { get; set; } = OneClickMode;

    public bool IsOneClickRedemption =>
        !string.Equals(RedemptionMode, ThreeStepMode, StringComparison.OrdinalIgnoreCase);

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

/// <summary>Cashier-facing explanations for POS failure reasons. Every reason the API can
/// return maps to a sentence stating the cause and the next action, so staff can act without
/// escalating; unknown reasons fall back to the raw code.</summary>
public static class PosFailureReasons
{
    public const string TerminalKeyMismatch = "TerminalKeyMismatch";
    public const string InvalidCodeFormat = "InvalidCodeFormat";
    public const string CodeExpired = "CodeExpired";
    public const string Forged = "Forged";
    public const string AlreadyUsed = "AlreadyUsed";
    public const string Expired = "Expired";
    public const string NotYetValid = "NotYetValid";
    public const string TransferPending = "TransferPending";
    public const string OutletNotAuthorized = "OutletNotAuthorized";
    public const string LockExpired = "LockExpired";
    public const string AlreadyComplete = "AlreadyComplete";
    public const string AmountExceedsValue = "AmountExceedsValue";
    public const string AlreadyReleased = "AlreadyReleased";
    public const string OutletMismatch = "OutletMismatch";
    public const string RateLimited = "RateLimited";
    public const string TerminalRejected = "TerminalRejected";
    public const string ServiceRejected = "ServiceRejected";

    /// <summary>Actionable sentence for known reasons; the raw reason otherwise; null stays null.</summary>
    public static string? Describe(string? reason, string? storeCode) => reason switch
    {
        null => null,
        TerminalKeyMismatch =>
            $"The API key stored on this terminal belongs to a different outlet than store {storeCode}. "
            + "Reset the terminal on the login screen and provision it with this store's Setup Key.",
        InvalidCodeFormat =>
            "Not a scannable voucher code. Serial numbers (VC-...) cannot be redeemed here — ask the customer "
            + "to open My Vouchers in the member app and show the rotating code (valid 2 minutes).",
        CodeExpired =>
            "The customer's rotating code expired (2-minute validity). Ask them to refresh My Vouchers "
            + "and scan the new code.",
        Forged =>
            "Code signature does not match our records — treat as counterfeit. Do not accept it; "
            + "escalate to a manager if the customer insists.",
        AlreadyUsed =>
            "This voucher was already redeemed, or another open transaction still holds its lock.",
        Expired =>
            "The voucher campaign has ended (past its expiry / valid-to date).",
        NotYetValid =>
            "The voucher is not active yet (its publish / valid-from date is in the future).",
        TransferPending =>
            "This voucher is on its way to another member as a gift and is reserved for them until they accept "
            + "it or the gift expires. Redemption stays blocked meanwhile — ask the customer to come back once "
            + "the gift is resolved, or check its status under BrandManager > Gifts.",
        OutletNotAuthorized =>
            "This store is not in the voucher's applicable stores — check the applicable-store list shown "
            + "with the voucher.",
        LockExpired =>
            "The lock on this voucher expired before commit. Verify the code again to take a new lock.",
        AlreadyComplete =>
            "This voucher was already completed in an earlier transaction.",
        AmountExceedsValue =>
            "The amount entered is more than this voucher is worth, so nothing was redeemed and no usage was "
            + "recorded. Correct the Amount Used field, then press Commit (or Redeem) — the voucher is still held "
            + "for this bill, so the customer does not need a new code.",
        AlreadyReleased =>
            "This voucher's lock was already released — nothing left to roll back.",
        OutletMismatch =>
            "This lock was opened by another store, so it cannot be released from here — the store that started "
            + "the transaction must roll it back, or it releases itself after 10 minutes.",
        RateLimited =>
            "This terminal has sent too many voucher requests in the last minute. Wait 60 seconds, then scan the "
            + "code once more — scans sent before then are rejected.",
        TerminalRejected =>
            "The NonCash service refused this terminal's API key, so no voucher can be checked until the terminal "
            + "is re-provisioned. Reset the terminal on the login screen and provision it with this store's Setup Key.",
        ServiceRejected =>
            "The NonCash service refused this request and returned no voucher result. Check the terminal's internet "
            + "connection and try once more; if it keeps happening the service may be down — tell the store manager "
            + "and note the voucher details by hand.",
        _ => reason
    };
}

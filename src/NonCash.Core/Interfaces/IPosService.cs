namespace NonCash.Core.Interfaces;

public interface IPosService
{
    /// <summary>
    /// Stateless verification of a voucher's validity at a given outlet.
    /// Does NOT mutate any state. Returns Valid + voucher info, or Invalid + reason.
    /// </summary>
    Task<PosVerifyResult> VerifyAsync(
        string voucherCode,
        Guid outletId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the voucher and atomically transitions it to InUse with a LockId.
    /// Idempotent: if the same (voucherId, outletId, billNumber) tuple already holds the lock,
    /// returns the existing LockId.
    /// </summary>
    Task<PosLockResult> LockAsync(
        string voucherCode,
        Guid outletId,
        string billNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits a previously-locked voucher: atomically flips InUse → Complete and records a VoucherUsage.
    /// Idempotent on TransactionId.
    /// </summary>
    Task<PosCommitResult> CommitAsync(
        Guid lockId,
        string transactionId,
        decimal amountUsed,
        Guid outletId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a previously-locked voucher (InUse → Pending). Compensating transaction for Lock.
    /// Does NOT create a VoucherUsage. Idempotent.
    /// </summary>
    Task<PosRollbackResult> RollbackAsync(
        Guid lockId,
        CancellationToken cancellationToken = default);
}

public record PosVerifyResult(
    string Status,
    string? Reason,
    PosVoucherInfo? VoucherInfo
);

public record PosLockResult(
    string Status,
    string? Reason,
    Guid? LockId,
    PosVoucherInfo? VoucherInfo
);

public record PosCommitResult(
    string Status,
    string? Message,
    string? Reason
);

public record PosRollbackResult(
    string Status,
    string? Message,
    string? Reason
);

public record PosVoucherInfo(
    decimal FaceValue,
    string ValueType,
    DateTime ExpiryDate,
    string BrandName,
    string SerialNo
);

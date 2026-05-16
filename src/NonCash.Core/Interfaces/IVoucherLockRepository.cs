using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IVoucherLockRepository
{
    /// <summary>
    /// Atomically tries to lock a voucher: sets UsageStatus from Pending to InUse with a new LockId.
    /// Returns the new LockId on success, or null if another process owns it.
    /// </summary>
    Task<Guid?> TryAcquireLockAsync(
        Guid voucherId,
        Guid outletId,
        string billNumber,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current voucher detail (used for idempotency checks).
    /// </summary>
    Task<VoucherPlanDetail?> FindByIdAsync(Guid voucherId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases all locks whose LockedAt + ttl has passed (resets them to Pending).
    /// Returns the number of locks released.
    /// </summary>
    Task<int> ReleaseExpiredLocksAsync(DateTime cutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an existing usage record by TransactionId (for idempotency check).
    /// </summary>
    Task<VoucherUsage?> FindUsageByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically commits a locked voucher: validates lock, transitions InUse to Complete,
    /// inserts a VoucherUsage record, and clears lock fields. Wrapped in a DB transaction.
    /// Returns CommitOutcome.Success on success, or a specific failure status.
    /// </summary>
    Task<CommitOutcome> CommitAsync(
        Guid lockId,
        string transactionId,
        decimal amountUsed,
        Guid posId,
        DateTime now,
        DateTime expiryCutoff,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically rolls back a locked voucher (InUse → Pending), clearing lock fields.
    /// Does NOT create any VoucherUsage record. Returns RollbackOutcome.
    /// </summary>
    Task<RollbackOutcome> RollbackAsync(
        Guid lockId,
        CancellationToken cancellationToken = default);
}

public enum CommitOutcome
{
    Success,
    LockExpired,
    LockNotFound,
    AlreadyComplete
}

public enum RollbackOutcome
{
    Success,
    AlreadyReleased,
    AlreadyComplete,
    LockNotFound
}

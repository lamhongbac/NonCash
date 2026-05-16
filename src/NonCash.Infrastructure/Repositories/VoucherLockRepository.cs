using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class VoucherLockRepository : IVoucherLockRepository
{
    private readonly ApplicationDbContext _context;

    public VoucherLockRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> TryAcquireLockAsync(
        Guid voucherId,
        Guid outletId,
        string billNumber,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var lockId = Guid.NewGuid();

        // Atomic conditional update: only when status is still Pending
        var rowsAffected = await _context.Set<VoucherPlanDetail>()
            .Where(v => v.Id == voucherId && v.UsageStatus == UsageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.UsageStatus, UsageStatus.InUse)
                .SetProperty(v => v.LockId, lockId)
                .SetProperty(v => v.LockedAt, now)
                .SetProperty(v => v.LockedOutletId, outletId)
                .SetProperty(v => v.BillNumber, billNumber),
                cancellationToken);

        return rowsAffected == 1 ? lockId : null;
    }

    public async Task<VoucherPlanDetail?> FindByIdAsync(Guid voucherId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<VoucherPlanDetail>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == voucherId, cancellationToken);
    }

    public async Task<int> ReleaseExpiredLocksAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        return await _context.Set<VoucherPlanDetail>()
            .Where(v => v.UsageStatus == UsageStatus.InUse
                     && v.LockedAt != null
                     && v.LockedAt < cutoff)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.UsageStatus, UsageStatus.Pending)
                .SetProperty(v => v.LockId, (Guid?)null)
                .SetProperty(v => v.LockedAt, (DateTime?)null)
                .SetProperty(v => v.LockedOutletId, (Guid?)null)
                .SetProperty(v => v.BillNumber, (string?)null),
                cancellationToken);
    }

    public async Task<VoucherUsage?> FindUsageByTransactionIdAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<VoucherUsage>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TransactionId == transactionId, cancellationToken);
    }

    public async Task<CommitOutcome> CommitAsync(
        Guid lockId,
        string transactionId,
        decimal amountUsed,
        Guid posId,
        DateTime now,
        DateTime expiryCutoff,
        CancellationToken cancellationToken = default)
    {
        // Resolve the locked voucher detail by LockId.
        var detail = await _context.Set<VoucherPlanDetail>()
            .FirstOrDefaultAsync(v => v.LockId == lockId, cancellationToken);

        if (detail == null)
        {
            return CommitOutcome.LockNotFound;
        }

        if (detail.UsageStatus == UsageStatus.Complete)
        {
            return CommitOutcome.AlreadyComplete;
        }

        if (detail.UsageStatus != UsageStatus.InUse
            || detail.LockedAt == null
            || detail.LockedAt < expiryCutoff)
        {
            return CommitOutcome.LockExpired;
        }

        // NFR2: wrap status transition + usage insert in a single transaction.
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Atomic conditional flip: InUse + matching LockId + not expired → Complete.
            var rowsAffected = await _context.Set<VoucherPlanDetail>()
                .Where(v => v.Id == detail.Id
                         && v.LockId == lockId
                         && v.UsageStatus == UsageStatus.InUse
                         && v.LockedAt != null
                         && v.LockedAt >= expiryCutoff)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.UsageStatus, UsageStatus.Complete)
                    .SetProperty(v => v.UsedDate, (DateTime?)now)
                    .SetProperty(v => v.LockId, (Guid?)null)
                    .SetProperty(v => v.LockedAt, (DateTime?)null)
                    .SetProperty(v => v.LockedOutletId, (Guid?)null)
                    .SetProperty(v => v.BillNumber, (string?)null)
                    .SetProperty(v => v.UpdatedAt, (DateTime?)now),
                    cancellationToken);

            if (rowsAffected != 1)
            {
                await tx.RollbackAsync(cancellationToken);
                return CommitOutcome.LockExpired;
            }

            var usage = new VoucherUsage
            {
                Id = Guid.NewGuid(),
                VoucherId = detail.Id,
                PosId = posId,
                TransactionId = transactionId,
                UsageDate = now,
                AmountUsed = amountUsed,
                CreatedAt = now
            };
            _context.Set<VoucherUsage>().Add(usage);
            await _context.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return CommitOutcome.Success;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<RollbackOutcome> RollbackAsync(
        Guid lockId,
        CancellationToken cancellationToken = default)
    {
        // Atomic conditional update: only flip InUse rows that still hold this lockId.
        var rowsAffected = await _context.Set<VoucherPlanDetail>()
            .Where(v => v.LockId == lockId && v.UsageStatus == UsageStatus.InUse)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.UsageStatus, UsageStatus.Pending)
                .SetProperty(v => v.LockId, (Guid?)null)
                .SetProperty(v => v.LockedAt, (DateTime?)null)
                .SetProperty(v => v.LockedOutletId, (Guid?)null)
                .SetProperty(v => v.BillNumber, (string?)null)
                .SetProperty(v => v.UpdatedAt, (DateTime?)DateTime.UtcNow),
                cancellationToken);

        if (rowsAffected == 1)
        {
            return RollbackOutcome.Success;
        }

        // No row affected: check if voucher was already released (idempotency) or completed.
        var current = await _context.Set<VoucherPlanDetail>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.LockId == lockId, cancellationToken);

        if (current == null)
        {
            // LockId not present on any row: either expired-cleaned-up or never existed.
            // Both cases mean the voucher is effectively released; report AlreadyReleased.
            return RollbackOutcome.AlreadyReleased;
        }

        if (current.UsageStatus == UsageStatus.Complete)
        {
            return RollbackOutcome.AlreadyComplete;
        }
        if (current.UsageStatus == UsageStatus.Pending)
        {
            return RollbackOutcome.AlreadyReleased;
        }

        return RollbackOutcome.LockNotFound;
    }
}

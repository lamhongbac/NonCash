using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class VoucherTransferRepository : IVoucherTransferRepository
{
    private readonly ApplicationDbContext _context;

    public VoucherTransferRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VoucherTransfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<VoucherTransfer>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<VoucherTransfer> AddAsync(VoucherTransfer transfer, CancellationToken cancellationToken = default)
    {
        transfer.CreatedAt = DateTime.UtcNow;
        await _context.Set<VoucherTransfer>().AddAsync(transfer, cancellationToken);
        return transfer;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<VoucherTransfer?> FindPendingByVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<VoucherTransfer>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.VoucherId == voucherId && t.Status == VoucherTransferStatus.PendingAcceptance,
                cancellationToken);
    }

    public async Task<TransferActionResult> AcceptAsync(
        Guid transferId,
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var rowsAffected = await _context.Set<VoucherTransfer>()
                .Where(t => t.Id == transferId
                         && t.RecipientId == recipientId
                         && t.Status == VoucherTransferStatus.PendingAcceptance)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, VoucherTransferStatus.Accepted)
                    .SetProperty(t => t.RespondedAt, (DateTime?)now)
                    .SetProperty(t => t.UpdatedAt, (DateTime?)now),
                    cancellationToken);

            if (rowsAffected != 1)
            {
                await tx.RollbackAsync(cancellationToken);
                return new TransferActionResult(false, ErrorCode: "AlreadyResolved", ErrorMessage: "Transfer was already resolved.");
            }

            var voucher = await _context.Set<VoucherPlanDetail>()
                .FirstAsync(v => v.TransferLockId == transferId, cancellationToken);

            voucher.MemberId = recipientId;
            voucher.TransferLockId = null;
            voucher.TransferLockedAt = null;
            voucher.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new TransferActionResult(true, Status: "Accepted", VoucherId: voucher.Id);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TransferActionResult> RejectAsync(
        Guid transferId,
        Guid recipientId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var rowsAffected = await _context.Set<VoucherTransfer>()
                .Where(t => t.Id == transferId
                         && t.RecipientId == recipientId
                         && t.Status == VoucherTransferStatus.PendingAcceptance)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, VoucherTransferStatus.Rejected)
                    .SetProperty(t => t.RespondedAt, (DateTime?)now)
                    .SetProperty(t => t.RejectReason, reason)
                    .SetProperty(t => t.UpdatedAt, (DateTime?)now),
                    cancellationToken);

            if (rowsAffected != 1)
            {
                await tx.RollbackAsync(cancellationToken);
                return new TransferActionResult(false, ErrorCode: "AlreadyResolved", ErrorMessage: "Transfer was already resolved.");
            }

            await ReleaseVoucherTransferLockAsync(transferId, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new TransferActionResult(true, Status: "Rejected");
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TransferActionResult> CancelAsync(
        Guid transferId,
        Guid senderId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var rowsAffected = await _context.Set<VoucherTransfer>()
                .Where(t => t.Id == transferId
                         && t.SenderId == senderId
                         && t.Status == VoucherTransferStatus.PendingAcceptance)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, VoucherTransferStatus.Cancelled)
                    .SetProperty(t => t.RespondedAt, (DateTime?)now)
                    .SetProperty(t => t.UpdatedAt, (DateTime?)now),
                    cancellationToken);

            if (rowsAffected != 1)
            {
                await tx.RollbackAsync(cancellationToken);
                return new TransferActionResult(false, ErrorCode: "AlreadyResolved", ErrorMessage: "Transfer was already resolved.");
            }

            await ReleaseVoucherTransferLockAsync(transferId, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new TransferActionResult(true, Status: "Cancelled");
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TransferActionResult?> EnsureNotExpiredAsync(
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rowsAffected = await _context.Set<VoucherTransfer>()
            .Where(t => t.Id == transferId
                     && t.Status == VoucherTransferStatus.PendingAcceptance
                     && t.ExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Status, VoucherTransferStatus.Expired)
                .SetProperty(t => t.RespondedAt, (DateTime?)now)
                .SetProperty(t => t.UpdatedAt, (DateTime?)now),
                cancellationToken);

        if (rowsAffected == 0)
            return null;

        await ReleaseVoucherTransferLockAsync(transferId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new TransferActionResult(false, Status: "Expired", ErrorCode: "AlreadyResolved", ErrorMessage: "Transfer has expired.");
    }

    public async Task<IReadOnlyList<TransferInboxItem>> GetInboxAsync(
        Guid recipientId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<VoucherTransfer>()
            .AsNoTracking()
            .Where(t => t.RecipientId == recipientId)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var items = await query
            .OrderByDescending(t => t.InitiatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.VoucherId,
                t.Voucher!.SerialNo,
                BrandName = t.Voucher.Parent!.Brand!.Name,
                t.Voucher.Parent.FaceValue,
                ValueType = t.Voucher.Parent.ValueType,
                t.Voucher.Parent.ExpiryDate,
                t.SenderId,
                SenderDisplayName = t.Sender!.FullName,
                t.Note,
                t.Status,
                t.InitiatedAt,
                t.ExpiresAt
            })
            .ToListAsync(cancellationToken);

        return items.Select(i => new TransferInboxItem(
            i.Id,
            i.VoucherId,
            i.SerialNo,
            i.BrandName,
            i.FaceValue,
            i.ValueType.ToString(),
            i.ExpiryDate,
            i.SenderId,
            i.SenderDisplayName,
            i.Note,
            i.Status,
            i.InitiatedAt,
            i.ExpiresAt)).ToList();
    }

    public async Task<IReadOnlyList<TransferOutboxItem>> GetOutboxAsync(
        Guid senderId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<VoucherTransfer>()
            .AsNoTracking()
            .Where(t => t.SenderId == senderId)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var items = await query
            .OrderByDescending(t => t.InitiatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.VoucherId,
                t.Voucher!.SerialNo,
                BrandName = t.Voucher.Parent!.Brand!.Name,
                t.Voucher.Parent.FaceValue,
                ValueType = t.Voucher.Parent.ValueType,
                t.Voucher.Parent.ExpiryDate,
                t.RecipientId,
                RecipientDisplayName = t.Recipient!.FullName,
                t.Note,
                t.Status,
                t.InitiatedAt,
                t.ExpiresAt,
                t.RespondedAt
            })
            .ToListAsync(cancellationToken);

        return items.Select(i => new TransferOutboxItem(
            i.Id,
            i.VoucherId,
            i.SerialNo,
            i.BrandName,
            i.FaceValue,
            i.ValueType.ToString(),
            i.ExpiryDate,
            i.RecipientId,
            i.RecipientDisplayName,
            i.Note,
            i.Status,
            i.InitiatedAt,
            i.ExpiresAt,
            i.RespondedAt)).ToList();
    }

    public async Task<int> SweepExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var expiredIds = await _context.Set<VoucherTransfer>()
                .Where(t => t.Status == VoucherTransferStatus.PendingAcceptance && t.ExpiresAt <= now)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            if (expiredIds.Count == 0)
            {
                await tx.CommitAsync(cancellationToken);
                return 0;
            }

            await _context.Set<VoucherTransfer>()
                .Where(t => expiredIds.Contains(t.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, VoucherTransferStatus.Expired)
                    .SetProperty(t => t.RespondedAt, (DateTime?)now)
                    .SetProperty(t => t.UpdatedAt, (DateTime?)now),
                    cancellationToken);

            await _context.Set<VoucherPlanDetail>()
                .Where(v => expiredIds.Contains(v.TransferLockId!.Value))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.TransferLockId, (Guid?)null)
                    .SetProperty(v => v.TransferLockedAt, (DateTime?)null)
                    .SetProperty(v => v.UpdatedAt, (DateTime?)now),
                    cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return expiredIds.Count;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ReleaseVoucherTransferLockAsync(Guid transferId, CancellationToken cancellationToken)
    {
        await _context.Set<VoucherPlanDetail>()
            .Where(v => v.TransferLockId == transferId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.TransferLockId, (Guid?)null)
                .SetProperty(v => v.TransferLockedAt, (DateTime?)null)
                .SetProperty(v => v.UpdatedAt, (DateTime?)DateTime.UtcNow),
                cancellationToken);
    }
}

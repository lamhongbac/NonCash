using System.Linq.Expressions;
using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IVoucherTransferRepository
{
    Task<VoucherTransfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VoucherTransfer> AddAsync(VoucherTransfer transfer, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherTransfer>> FindAsync(Expression<Func<VoucherTransfer, bool>> predicate, CancellationToken cancellationToken = default);

    Task<VoucherTransfer?> FindPendingByVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default);

    Task<TransferActionResult> AcceptAsync(
        Guid transferId,
        Guid recipientId,
        string? recipientNote = null,
        CancellationToken cancellationToken = default);

    Task<TransferActionResult> RejectAsync(
        Guid transferId,
        Guid recipientId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<TransferActionResult> CancelAsync(
        Guid transferId,
        Guid senderId,
        CancellationToken cancellationToken = default);

    Task<TransferActionResult?> EnsureNotExpiredAsync(
        Guid transferId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransferInboxItem>> GetInboxAsync(
        Guid recipientId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransferOutboxItem>> GetOutboxAsync(
        Guid senderId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<int> SweepExpiredAsync(DateTime now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BrandGiftItem>> GetBrandGiftsAsync(
        Guid? brandId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>CR-2026-09-10-31: stages a message so it is written by the caller's next
    /// SaveChangesAsync — inside the same transaction as the gift write that produced it.</summary>
    void AddMessage(GiftMessage message);

    /// <summary>CR-2026-09-10-31: every message of a gift, oldest first.</summary>
    Task<IReadOnlyList<GiftMessageItem>> GetThreadAsync(Guid transferId, CancellationToken cancellationToken = default);

    /// <summary>CR-2026-09-10-31: only the kinds a brand may read, oldest first. A brandId that
    /// does not own the gift's voucher returns an empty list.</summary>
    Task<IReadOnlyList<GiftMessageItem>> GetBrandVisibleThreadAsync(
        Guid transferId,
        Guid? brandId,
        CancellationToken cancellationToken = default);

    /// <summary>CR-2026-09-10-31: marks the messages addressed to a reader as read.</summary>
    Task MarkMessagesReadAsync(Guid transferId, Guid readerMemberId, CancellationToken cancellationToken = default);

    Task<int> CountMessagesAsync(Guid transferId, CancellationToken cancellationToken = default);

    Task<int> CountMessagesByAuthorSinceAsync(Guid authorMemberId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}

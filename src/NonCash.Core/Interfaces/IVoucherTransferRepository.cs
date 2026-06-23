using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IVoucherTransferRepository
{
    Task<VoucherTransfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VoucherTransfer> AddAsync(VoucherTransfer transfer, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<VoucherTransfer?> FindPendingByVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default);

    Task<TransferActionResult> AcceptAsync(
        Guid transferId,
        Guid recipientId,
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
}

using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.IntegrationTests.Fixtures;

/// <summary>
/// Inert <see cref="IVoucherTransferService"/> for tests that exercise a flow which never reaches
/// the gift pipeline (for example a registration that is refused before activation).
/// </summary>
public sealed class StubVoucherTransferService : IVoucherTransferService
{
    public Task<InitiateTransferResult> InitiateAsync(
        Guid senderId,
        Guid voucherId,
        string? recipientPhone,
        string? recipientMemberId,
        string? note,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new InitiateTransferResult(false, ErrorCode: "NotSupported"));

    public Task<TransferActionResult> AcceptAsync(
        Guid transferId,
        Guid recipientId,
        string? recipientNote = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TransferActionResult(false, ErrorCode: "NotSupported"));

    public Task<TransferActionResult> RejectAsync(
        Guid transferId,
        Guid recipientId,
        string? reason,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TransferActionResult(false, ErrorCode: "NotSupported"));

    public Task<TransferActionResult> CancelAsync(
        Guid transferId,
        Guid senderId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TransferActionResult(false, ErrorCode: "NotSupported"));

    public Task<IReadOnlyList<TransferInboxItem>> GetInboxAsync(
        Guid recipientId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TransferInboxItem>>(Array.Empty<TransferInboxItem>());

    public Task<IReadOnlyList<TransferOutboxItem>> GetOutboxAsync(
        Guid senderId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TransferOutboxItem>>(Array.Empty<TransferOutboxItem>());

    public Task<int> SweepExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task NotifyPendingGiftsAsync(Guid memberId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<BrandGiftItem>> GetBrandGiftsAsync(
        Guid? brandId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BrandGiftItem>>(Array.Empty<BrandGiftItem>());

    public Task<GiftThreadResult> GetThreadAsync(
        Guid transferId,
        Guid viewerMemberId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new GiftThreadResult(false, ErrorCode: "NotSupported"));

    public Task<GiftMessageResult> PostMessageAsync(
        Guid transferId,
        Guid authorMemberId,
        string? body,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new GiftMessageResult(false, ErrorCode: "NotSupported"));

    public Task<IReadOnlyList<GiftMessageItem>> GetBrandMessagesAsync(
        Guid transferId,
        Guid? brandId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GiftMessageItem>>(Array.Empty<GiftMessageItem>());
}

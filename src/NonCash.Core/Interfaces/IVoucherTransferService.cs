using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IVoucherTransferService
{
    Task<InitiateTransferResult> InitiateAsync(
        Guid senderId,
        Guid voucherId,
        string? recipientPhone,
        string? recipientMemberId,
        string? note,
        CancellationToken cancellationToken = default);

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
}

public record InitiateTransferResult(
    bool Success,
    Guid? TransferId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record TransferActionResult(
    bool Success,
    string? Status = null,
    Guid? VoucherId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record TransferInboxItem(
    Guid TransferId,
    Guid VoucherId,
    string SerialNo,
    string BrandName,
    decimal FaceValue,
    string? ValueType,
    DateTime ExpiryDate,
    Guid SenderId,
    string SenderDisplayName,
    string? Note,
    VoucherTransferStatus Status,
    DateTime InitiatedAt,
    DateTime ExpiresAt);

public record TransferOutboxItem(
    Guid TransferId,
    Guid VoucherId,
    string SerialNo,
    string BrandName,
    decimal FaceValue,
    string? ValueType,
    DateTime ExpiryDate,
    Guid RecipientId,
    string RecipientDisplayName,
    string? Note,
    VoucherTransferStatus Status,
    DateTime InitiatedAt,
    DateTime ExpiresAt,
    DateTime? RespondedAt);

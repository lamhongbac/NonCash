using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface ITransferService
{
    Task<TransferResult> TransferAsync(
        Guid fromMemberId,
        IReadOnlyList<Guid> voucherIds,
        IReadOnlyList<string> recipientPhones,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransferHistoryItem>> GetOutgoingHistoryAsync(
        Guid fromMemberId,
        CancellationToken cancellationToken = default);
}

public record TransferResult(
    bool Success,
    int TransferredCount = 0,
    int SkippedCount = 0,
    IReadOnlyList<TransferSkipped>? SkippedRecords = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record TransferSkipped(string PhoneNumber, Guid VoucherId, string Reason);

public record TransferHistoryItem(
    Guid VoucherId,
    string SerialNo,
    string RecipientPhone,
    DateTime TransferredAt);

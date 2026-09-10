using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface ITransferService
{
    /// <param name="note">CR-2026-09-10-31: one optional message that travels with every gift in this send.</param>
    Task<TransferResult> TransferAsync(
        Guid fromMemberId,
        IReadOnlyList<Guid> voucherIds,
        IReadOnlyList<string> recipientPhones,
        string? note = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransferHistoryItem>> GetOutgoingHistoryAsync(
        Guid fromMemberId,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a bulk gift send. CR-2026-09-10-30: <see cref="TransferredCount"/> counts
/// gifts that were sent and are waiting for the recipient, not ownership moves — a gift only
/// changes owner when the recipient accepts it.</summary>
public record TransferResult(
    bool Success,
    int TransferredCount = 0,
    int SkippedCount = 0,
    IReadOnlyList<TransferSkipped>? SkippedRecords = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    IReadOnlyList<GiftDelivery>? Deliveries = null);

public record TransferSkipped(string PhoneNumber, Guid VoucherId, string Reason);

/// <summary>CR-2026-09-10-30 row 2: per-gift message shown to the sender straight after sending,
/// so they always know whether the recipient was emailed or the gift is being held.</summary>
public record GiftDelivery(
    Guid VoucherId,
    Guid? TransferId,
    string RecipientPhone,
    string? RecipientName,
    string DeliveryStatus,
    string Message);

public record TransferHistoryItem(
    Guid VoucherId,
    string SerialNo,
    string RecipientPhone,
    DateTime TransferredAt);

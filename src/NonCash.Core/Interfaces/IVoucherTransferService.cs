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

    /// <summary>CR-2026-09-10-30: expires overdue gifts and tells each sender their voucher is
    /// back in their wallet. Returns the number of gifts expired.</summary>
    Task<int> SweepExpiredAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>CR-2026-09-10-30: sends the accept-link email for every gift still waiting on a
    /// member who just registered — placeholders have no email until then.</summary>
    Task NotifyPendingGiftsAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>CR-2026-09-10-30 row 6: brand-scoped gift log for BrandManager/Admin review.
    /// A null brandId returns gifts of every brand (Admin only).</summary>
    Task<IReadOnlyList<BrandGiftItem>> GetBrandGiftsAsync(
        Guid? brandId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>CR-2026-09-10-31: the whole conversation of one gift, as seen by one of its two
    /// participants. Reading it also marks the messages addressed to the viewer as read.</summary>
    Task<GiftThreadResult> GetThreadAsync(
        Guid transferId,
        Guid viewerMemberId,
        CancellationToken cancellationToken = default);

    /// <summary>CR-2026-09-10-31: a member adds a message to a gift's conversation. Only the sender
    /// and the recipient may write, and only while the gift is on its way or already accepted.</summary>
    Task<GiftMessageResult> PostMessageAsync(
        Guid transferId,
        Guid authorMemberId,
        string? body,
        CancellationToken cancellationToken = default);

    /// <summary>CR-2026-09-10-31: the brand-visible part of a gift's conversation — the note that
    /// travelled with the voucher and the reply it produced. Private follow-ups are never returned.
    /// A brandId that does not own the voucher yields an empty list.</summary>
    Task<IReadOnlyList<GiftMessageItem>> GetBrandMessagesAsync(
        Guid transferId,
        Guid? brandId,
        CancellationToken cancellationToken = default);
}

/// <summary>Delivery outcomes reported back to the gift sender (CR-2026-09-10-30 row 2).</summary>
public static class GiftDeliveryStatus
{
    /// <summary>Recipient has an email: accept link sent, we will report when they decide.</summary>
    public const string Emailed = "Emailed";

    /// <summary>Recipient exists but has no email: gift is held, they must sign in by phone.</summary>
    public const string HoldingNoEmail = "HoldingNoEmail";

    /// <summary>Recipient is not on NonCash yet: gift is held until they register.</summary>
    public const string HoldingNotRegistered = "HoldingNotRegistered";
}

public record InitiateTransferResult(
    bool Success,
    Guid? TransferId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? DeliveryStatus = null,
    string? RecipientPhone = null,
    string? RecipientName = null,
    string? Message = null);

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
    DateTime ExpiresAt,
    DateTime? RespondedAt = null,
    int MessageCount = 0,
    DateTime? LastMessageAt = null,
    int UnreadCount = 0);

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
    DateTime? RespondedAt,
    string? RecipientNote = null,
    string? RejectReason = null,
    int MessageCount = 0,
    DateTime? LastMessageAt = null,
    int UnreadCount = 0);

/// <summary>CR-2026-09-10-30 row 6: one line of the brand's gift log — who sent what to whom,
/// when, and how it ended.</summary>
public record BrandGiftItem(
    Guid TransferId,
    Guid VoucherId,
    string SerialNo,
    string BrandName,
    decimal FaceValue,
    string? ValueType,
    string SenderName,
    string SenderPhone,
    string RecipientName,
    string RecipientPhone,
    string? Note,
    string? RecipientNote,
    string? RejectReason,
    VoucherTransferStatus Status,
    DateTime InitiatedAt,
    DateTime ExpiresAt,
    DateTime? RespondedAt,
    int MessageCount = 0,
    DateTime? LastMessageAt = null);

/// <summary>CR-2026-09-10-31: one message of a gift's conversation.</summary>
public record GiftMessageItem(
    Guid MessageId,
    Guid TransferId,
    Guid AuthorMemberId,
    string AuthorDisplayName,
    GiftMessageDirection Direction,
    GiftMessageKind Kind,
    string Body,
    DateTime SentAt,
    bool IsRead);

/// <summary>CR-2026-09-10-31: what a participant sees when they open a gift's conversation —
/// the thread, plus whether they may still write to it and, if not, why.</summary>
public record GiftThreadResult(
    bool Success,
    IReadOnlyList<GiftMessageItem>? Messages = null,
    string? Status = null,
    bool CanReply = false,
    string? ClosedReason = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>CR-2026-09-10-31: outcome of posting a message to a gift's conversation.</summary>
public record GiftMessageResult(
    bool Success,
    GiftMessageItem? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

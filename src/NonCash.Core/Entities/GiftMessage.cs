namespace NonCash.Core.Entities;

/// <summary>Who wrote a gift message, seen from the gift itself.</summary>
public enum GiftMessageDirection
{
    FromSender,
    FromRecipient
}

/// <summary>
/// CR-2026-09-10-31: what a gift message is. The first three kinds are written by the gift flow
/// itself (the note that travels with the gift, the thank-you, the reason for declining);
/// <see cref="FollowUp"/> is a free message one participant sends to the other afterwards.
/// </summary>
public enum GiftMessageKind
{
    GiftNote,
    ThankYou,
    DeclineReason,
    FollowUp
}

/// <summary>
/// One message in a gift's conversation. A gift keeps its whole thread so both people — and, for
/// the three public kinds, the brand that issued the voucher — can read it back later.
/// </summary>
public class GiftMessage : BaseEntity
{
    public Guid TransferId { get; set; }
    public Guid AuthorMemberId { get; set; }
    public Guid RecipientMemberId { get; set; }
    public GiftMessageDirection Direction { get; set; }
    public GiftMessageKind Kind { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// When the message was logically sent. Equals the insert time for live traffic; for rows the
    /// migration backfilled from the old note columns it is the gift's own timestamp, while
    /// <see cref="BaseEntity.CreatedAt"/> stays the migration time.
    /// </summary>
    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public VoucherTransfer? Transfer { get; set; }
    public MemberAccount? Author { get; set; }
    public MemberAccount? Recipient { get; set; }

    /// <summary>The one way a message is created, so every write path fills the same fields.</summary>
    public static GiftMessage Create(
        Guid transferId,
        Guid authorMemberId,
        Guid recipientMemberId,
        GiftMessageDirection direction,
        GiftMessageKind kind,
        string body,
        DateTime sentAt) => new()
    {
        TransferId = transferId,
        AuthorMemberId = authorMemberId,
        RecipientMemberId = recipientMemberId,
        Direction = direction,
        Kind = kind,
        Body = body.Trim(),
        SentAt = sentAt,
        IsRead = false
    };
}

/// <summary>CR-2026-09-10-31: the limits and visibility rules of a gift conversation.</summary>
public static class GiftMessagePolicy
{
    /// <summary>Hard bound, matching the column. The member UI counts down from 280.</summary>
    public const int MaxBodyLength = 500;

    /// <summary>Most messages one gift can hold, so a thread stays a memory and not a mailbox.</summary>
    public const int MaxMessagesPerGift = 20;

    /// <summary>Most messages one member may post across all gifts in an hour.</summary>
    public const int MaxMessagesPerHour = 10;

    /// <summary>
    /// A conversation stays open while the gift is still on its way and after it has been accepted.
    /// It closes when the gift was declined, expired or cancelled — the voucher went back to the
    /// sender, and a closed thread cannot be used to keep contacting someone who said no.
    /// </summary>
    public static bool IsOpenForFollowUp(VoucherTransferStatus status) =>
        status is VoucherTransferStatus.PendingAcceptance or VoucherTransferStatus.Accepted;

    /// <summary>
    /// A brand may read the note that travelled with its own voucher and the reply it produced.
    /// Follow-up chatter belongs to the two members alone.
    /// </summary>
    public static bool IsBrandVisible(GiftMessageKind kind) => kind != GiftMessageKind.FollowUp;
}

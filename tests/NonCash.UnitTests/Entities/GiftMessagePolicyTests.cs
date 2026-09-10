using FluentAssertions;
using NonCash.Core.Entities;

namespace NonCash.UnitTests.Entities;

/// <summary>
/// CR-2026-09-10-31 row 5: the rules that decide whether a gift's conversation is still open, and
/// how much of it the brand may see. The length bound is also a contract with the migration that
/// created gift_messages.body, so changing it without a migration must fail here.
/// </summary>
public class GiftMessagePolicyTests
{
    [Theory]
    [InlineData(VoucherTransferStatus.PendingAcceptance, true)]
    [InlineData(VoucherTransferStatus.Accepted, true)]
    [InlineData(VoucherTransferStatus.Rejected, false)]
    [InlineData(VoucherTransferStatus.Expired, false)]
    [InlineData(VoucherTransferStatus.Cancelled, false)]
    public void FollowUp_IsOpenOnlyWhileTheGiftIsOnItsWayOrAlreadyAccepted(
        VoucherTransferStatus status, bool expected) =>
        GiftMessagePolicy.IsOpenForFollowUp(status).Should().Be(expected);

    [Theory]
    [InlineData(GiftMessageKind.GiftNote, true)]
    [InlineData(GiftMessageKind.ThankYou, true)]
    [InlineData(GiftMessageKind.DeclineReason, true)]
    [InlineData(GiftMessageKind.FollowUp, false)]
    public void BrandSeesEverythingExceptThePrivateFollowUps(GiftMessageKind kind, bool expected) =>
        GiftMessagePolicy.IsBrandVisible(kind).Should().Be(expected);

    [Fact]
    public void BodyLimit_MatchesTheColumnTheMigrationCreated() =>
        GiftMessagePolicy.MaxBodyLength.Should().Be(500);

    [Fact]
    public void Caps_AreTheAgreedOnesPerGiftAndPerHour()
    {
        GiftMessagePolicy.MaxMessagesPerGift.Should().Be(20);
        GiftMessagePolicy.MaxMessagesPerHour.Should().Be(10);
    }

    [Fact]
    public void Create_TrimsTheBodyAndStartsUnread()
    {
        var transferId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var sentAt = new DateTime(2026, 9, 10, 5, 0, 0, DateTimeKind.Utc);

        var message = GiftMessage.Create(
            transferId, authorId, readerId,
            GiftMessageDirection.FromSender, GiftMessageKind.FollowUp, "  See you Saturday  ", sentAt);

        message.TransferId.Should().Be(transferId);
        message.AuthorMemberId.Should().Be(authorId);
        message.RecipientMemberId.Should().Be(readerId);
        message.Direction.Should().Be(GiftMessageDirection.FromSender);
        message.Kind.Should().Be(GiftMessageKind.FollowUp);
        message.Body.Should().Be("See you Saturday");
        message.SentAt.Should().Be(sentAt);
        message.IsRead.Should().BeFalse();
        message.ReadAt.Should().BeNull();
    }
}

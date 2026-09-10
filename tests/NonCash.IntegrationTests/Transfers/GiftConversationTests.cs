using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Repositories;
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.Transfers;

/// <summary>
/// CR-2026-09-10-31: every gift keeps its own conversation. The note that travelled with the gift,
/// the thank-you or the decline reason it produced, and any follow-up the two of them add later are
/// rows in gift_messages — the three summary columns on voucher_transfers stay in step with them.
/// </summary>
public class GiftConversationTests : IDisposable
{
    private const string BobPhone = "0909222222";
    private const string ThankYou = "Cảm ơn món quà của bạn!";

    private readonly TransferAcceptanceTestFixture _fixture = new();

    /// <summary>The wallet's "Gửi Quà" button posts here: one voucher per phone number.</summary>
    private TransferService BulkSender() => new(
        _fixture.VoucherRepository,
        _fixture.CustomerRepository,
        _fixture.MemberRepository,
        new Repository<VoucherDistribution>(_fixture.Context),
        _fixture.TransferService);

    // ----- Row 4: the three lines the system writes on its own -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task SendWithNote_StartsTheConversationAndKeepsTheSummaryColumnInStep()
    {
        // Act
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "Happy birthday!");

        // Assert — the note is a message addressed to the recipient, unread until they open it
        var row = await SingleMessageAsync(initiated.TransferId!.Value);
        row.Kind.Should().Be(GiftMessageKind.GiftNote);
        row.Direction.Should().Be(GiftMessageDirection.FromSender);
        row.AuthorMemberId.Should().Be(_fixture.AliceMemberId);
        row.RecipientMemberId.Should().Be(_fixture.BobMemberId);
        row.Body.Should().Be("Happy birthday!");
        row.IsRead.Should().BeFalse();
        row.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));

        // Row 2: the denormalized list summary still carries the same words
        var transfer = await ReloadTransferAsync(initiated.TransferId.Value);
        transfer.Note.Should().Be("Happy birthday!");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task SendWithoutNote_LeavesTheConversationEmpty()
    {
        // Act
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "   ");

        // Assert — blank words are not stored as an empty message
        initiated.Success.Should().BeTrue();
        (await _fixture.Context.GiftMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task AcceptWithThankYou_AddsTheReplyAndKeepsTheSummaryColumnInStep()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "For you");

        // Act
        var accepted = await _fixture.TransferService.AcceptAsync(
            initiated.TransferId!.Value, _fixture.BobMemberId, ThankYou);

        // Assert
        accepted.Success.Should().BeTrue();
        var reply = await _fixture.Context.GiftMessages.AsNoTracking()
            .SingleAsync(m => m.TransferId == initiated.TransferId && m.Kind == GiftMessageKind.ThankYou);
        reply.Direction.Should().Be(GiftMessageDirection.FromRecipient);
        reply.AuthorMemberId.Should().Be(_fixture.BobMemberId);
        reply.RecipientMemberId.Should().Be(_fixture.AliceMemberId);
        reply.Body.Should().Be(ThankYou);

        // Both lines belong to the same conversation, and the summary column still matches
        (await MessageCountAsync(initiated.TransferId.Value)).Should().Be(2);
        var transfer = await ReloadTransferAsync(initiated.TransferId.Value);
        transfer.RecipientNote.Should().Be(ThankYou);
        transfer.Status.Should().Be(VoucherTransferStatus.Accepted);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task DeclineWithReason_AddsTheReasonToTheConversation()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);

        // Act
        var declined = await _fixture.TransferService.RejectAsync(
            initiated.TransferId!.Value, _fixture.BobMemberId, "I already have one");

        // Assert
        declined.Success.Should().BeTrue();
        var row = await SingleMessageAsync(initiated.TransferId.Value);
        row.Kind.Should().Be(GiftMessageKind.DeclineReason);
        row.Direction.Should().Be(GiftMessageDirection.FromRecipient);
        row.AuthorMemberId.Should().Be(_fixture.BobMemberId);
        row.RecipientMemberId.Should().Be(_fixture.AliceMemberId);
        row.Body.Should().Be("I already have one");
    }

    // ----- Row 4: the follow-up the two of them write themselves -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task FollowUpWhileTheGiftIsWaiting_IsStoredAndAnnouncedToTheOtherSide()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);

        // Act
        var posted = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId!.Value, _fixture.AliceMemberId, "It is reserved for you for 7 days.");

        // Assert
        posted.Success.Should().BeTrue();
        posted.Message!.Kind.Should().Be(GiftMessageKind.FollowUp);
        posted.Message.Direction.Should().Be(GiftMessageDirection.FromSender);
        posted.Message.AuthorDisplayName.Should().Be("Alice Sender");
        posted.Message.IsRead.Should().BeFalse();

        var row = await SingleMessageAsync(initiated.TransferId.Value);
        row.RecipientMemberId.Should().Be(_fixture.BobMemberId);

        var told = _fixture.Notifications.GiftMessages.Should().ContainSingle().Subject;
        told.RecipientEmail.Should().Be("bob@test.com");
        told.RecipientName.Should().Be("Bob Receiver");
        told.AuthorName.Should().Be("Alice Sender");
        told.Body.Should().Be("It is reserved for you for 7 days.");
        told.VoucherName.Should().Be("Test Coffee voucher");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task FollowUpAfterAcceptance_IsStillAllowed()
    {
        // Arrange — decision A: an accepted gift keeps its conversation open
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "For you");
        await _fixture.TransferService.AcceptAsync(initiated.TransferId!.Value, _fixture.BobMemberId, ThankYou);

        // Act
        var posted = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId.Value, _fixture.BobMemberId, "See you on Saturday!");

        // Assert
        posted.Success.Should().BeTrue();
        posted.Message!.Direction.Should().Be(GiftMessageDirection.FromRecipient);
        (await MessageCountAsync(initiated.TransferId.Value)).Should().Be(3);

        var thread = await _fixture.TransferService.GetThreadAsync(initiated.TransferId.Value, _fixture.AliceMemberId);
        thread.CanReply.Should().BeTrue();
        thread.ClosedReason.Should().BeNull();
        thread.Status.Should().Be(VoucherTransferStatus.Accepted.ToString());
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task FollowUpAfterADecline_IsRefusedWithTheCauseAndTheWayForward()
    {
        // Arrange — decision B: a declined gift's conversation is closed
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);
        await _fixture.TransferService.RejectAsync(initiated.TransferId!.Value, _fixture.BobMemberId, "I already have one");

        // Act
        var posted = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId.Value, _fixture.AliceMemberId, "Are you sure?");
        var thread = await _fixture.TransferService.GetThreadAsync(initiated.TransferId.Value, _fixture.AliceMemberId);

        // Assert
        posted.Success.Should().BeFalse();
        posted.ErrorCode.Should().Be("ConversationClosed");
        posted.ErrorMessage.Should().Contain("declined").And.Contain("send a new gift");

        // The thread is still readable — it is a memory — but nobody can add to it
        thread.Success.Should().BeTrue();
        thread.CanReply.Should().BeFalse();
        thread.ClosedReason.Should().Contain("declined");
        thread.Messages.Should().ContainSingle().Which.Kind.Should().Be(GiftMessageKind.DeclineReason);
        (await MessageCountAsync(initiated.TransferId.Value)).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task FollowUpAfterTheGiftExpired_IsRefusedAndSaysTheVoucherIsBack()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);
        var transfer = await _fixture.Context.VoucherTransfers.SingleAsync(t => t.Id == initiated.TransferId);
        transfer.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await _fixture.Context.SaveChangesAsync();

        // Act
        var posted = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId!.Value, _fixture.BobMemberId, "Sorry I am late!");

        // Assert
        posted.Success.Should().BeFalse();
        posted.ErrorCode.Should().Be("ConversationClosed");
        posted.ErrorMessage.Should().Contain("Nobody accepted").And.Contain("back in the sender's wallet");
        (await MessageCountAsync(initiated.TransferId.Value)).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task FollowUpAfterTheSenderCancelled_IsRefused()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);
        await _fixture.TransferService.CancelAsync(initiated.TransferId!.Value, _fixture.AliceMemberId);

        // Act
        var posted = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId.Value, _fixture.BobMemberId, "Wait, I wanted it!");

        // Assert
        posted.Success.Should().BeFalse();
        posted.ErrorCode.Should().Be("ConversationClosed");
        posted.ErrorMessage.Should().Contain("cancelled");
    }

    // ----- Row 5: who may write, and how much -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task ThreadOfSomeoneOutsideTheGift_CanBeNeitherReadNorWritten()
    {
        // Arrange
        var outsiderId = await SeedOutsiderAsync();
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "For you");

        // Act
        var read = await _fixture.TransferService.GetThreadAsync(initiated.TransferId!.Value, outsiderId);
        var write = await _fixture.TransferService.PostMessageAsync(initiated.TransferId.Value, outsiderId, "Let me in");

        // Assert
        read.Success.Should().BeFalse();
        read.ErrorCode.Should().Be("Forbidden");
        read.ErrorMessage.Should().Contain("Only the two people in this gift");
        read.Messages.Should().BeNull();

        write.Success.Should().BeFalse();
        write.ErrorCode.Should().Be("Forbidden");
        (await MessageCountAsync(initiated.TransferId.Value)).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task BlankOrOverlongMessage_IsRefusedBeforeAnythingIsStored()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);

        // Act
        var blank = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId!.Value, _fixture.AliceMemberId, "   ");
        var overlong = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId.Value, _fixture.AliceMemberId, new string('a', GiftMessagePolicy.MaxBodyLength + 1));

        // Assert
        blank.ErrorCode.Should().Be("Validation");
        blank.ErrorMessage.Should().Contain("empty");

        overlong.ErrorCode.Should().Be("Validation");
        overlong.ErrorMessage.Should().Contain(GiftMessagePolicy.MaxBodyLength.ToString())
            .And.Contain("Shorten it");

        (await MessageCountAsync(initiated.TransferId.Value)).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task Conversation_StopsAtThePerGiftCap()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);
        await SeedMessagesAsync(initiated.TransferId!.Value, GiftMessagePolicy.MaxMessagesPerGift);

        // Act
        var posted = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId.Value, _fixture.AliceMemberId, "One more line");
        var thread = await _fixture.TransferService.GetThreadAsync(initiated.TransferId.Value, _fixture.AliceMemberId);

        // Assert
        posted.Success.Should().BeFalse();
        posted.ErrorCode.Should().Be("MessageLimitReached");
        posted.ErrorMessage.Should().Contain(GiftMessagePolicy.MaxMessagesPerGift.ToString())
            .And.Contain("still read the conversation");

        thread.Success.Should().BeTrue();
        thread.CanReply.Should().BeFalse();
        thread.ClosedReason.Should().Contain(GiftMessagePolicy.MaxMessagesPerGift.ToString());
        thread.Messages.Should().HaveCount(GiftMessagePolicy.MaxMessagesPerGift);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task HourlyLimit_StopsOneMemberWithoutTouchingTheOther()
    {
        // Arrange — Alice has already written the hourly allowance
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);
        await SeedMessagesAsync(initiated.TransferId!.Value, GiftMessagePolicy.MaxMessagesPerHour);

        // Act
        var alice = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId.Value, _fixture.AliceMemberId, "One more line");
        var bob = await _fixture.TransferService.PostMessageAsync(
            initiated.TransferId.Value, _fixture.BobMemberId, "I can still write");

        // Assert
        alice.Success.Should().BeFalse();
        alice.ErrorCode.Should().Be("RateLimited");
        alice.ErrorMessage.Should().Contain("hourly limit").And.Contain("Wait a few minutes");

        bob.Success.Should().BeTrue();
        bob.Message!.Direction.Should().Be(GiftMessageDirection.FromRecipient);
    }

    // ----- Row 7: the counts the lists show -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task OpeningTheThread_ClearsTheRecipientUnreadCount()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "For you");
        var before = (await _fixture.TransferService.GetInboxAsync(_fixture.BobMemberId)).Should().ContainSingle().Subject;
        before.MessageCount.Should().Be(1);
        before.UnreadCount.Should().Be(1);
        before.LastMessageAt.Should().NotBeNull();

        // Act
        var thread = await _fixture.TransferService.GetThreadAsync(initiated.TransferId!.Value, _fixture.BobMemberId);

        // Assert
        thread.Messages.Should().ContainSingle().Which.IsRead.Should().BeTrue();
        var after = (await _fixture.TransferService.GetInboxAsync(_fixture.BobMemberId)).Should().ContainSingle().Subject;
        after.UnreadCount.Should().Be(0);
        after.MessageCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task SenderOutboxCountsTheReplyAsUnreadUntilTheyOpenIt()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "For you");
        await _fixture.TransferService.AcceptAsync(initiated.TransferId!.Value, _fixture.BobMemberId, ThankYou);

        // Act
        var before = (await _fixture.TransferService.GetOutboxAsync(_fixture.AliceMemberId)).Should().ContainSingle().Subject;

        // Assert — the note is Alice's own, so only the thank-you counts as unread for her
        before.MessageCount.Should().Be(2);
        before.UnreadCount.Should().Be(1);
        before.LastMessageAt.Should().NotBeNull();

        await _fixture.TransferService.GetThreadAsync(initiated.TransferId.Value, _fixture.AliceMemberId);
        var after = (await _fixture.TransferService.GetOutboxAsync(_fixture.AliceMemberId)).Should().ContainSingle().Subject;
        after.UnreadCount.Should().Be(0);
    }

    // ----- Row 10: what the brand may see -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task BrandThread_ShowsTheNoteAndTheReply_ButNeverThePrivateFollowUps()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "Happy birthday");
        await _fixture.TransferService.AcceptAsync(initiated.TransferId!.Value, _fixture.BobMemberId, ThankYou);
        await _fixture.TransferService.PostMessageAsync(initiated.TransferId.Value, _fixture.BobMemberId, "See you Saturday");

        // Act
        var ownBrand = await _fixture.TransferService.GetBrandMessagesAsync(initiated.TransferId.Value, _fixture.BrandId);
        var otherBrand = await _fixture.TransferService.GetBrandMessagesAsync(initiated.TransferId.Value, Guid.NewGuid());

        // Assert
        ownBrand.Should().HaveCount(2);
        ownBrand.Should().Contain(m => m.Kind == GiftMessageKind.GiftNote && m.Body == "Happy birthday");
        ownBrand.Should().Contain(m => m.Kind == GiftMessageKind.ThankYou && m.Body == ThankYou);
        ownBrand.Should().NotContain(m => m.Kind == GiftMessageKind.FollowUp);
        ownBrand.Should().OnlyContain(m => m.AuthorDisplayName.Length > 0);

        otherBrand.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task BrandGiftLogCountsOnlyTheMessagesTheBrandMaySee()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "Happy birthday");
        await _fixture.TransferService.AcceptAsync(initiated.TransferId!.Value, _fixture.BobMemberId, ThankYou);
        await _fixture.TransferService.PostMessageAsync(initiated.TransferId.Value, _fixture.BobMemberId, "See you Saturday");

        // Act
        var row = (await _fixture.TransferService.GetBrandGiftsAsync(_fixture.BrandId)).Should().ContainSingle().Subject;

        // Assert — three messages exist, but only two of them are the brand's business
        row.MessageCount.Should().Be(2);
        row.LastMessageAt.Should().NotBeNull();
        (await MessageCountAsync(initiated.TransferId.Value)).Should().Be(3);
    }

    // ----- The bulk send path carries the note too -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task BulkSend_CarriesTheSendersNoteIntoTheConversation()
    {
        // Act
        var result = await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { BobPhone },
            "For you, enjoy!");

        // Assert
        result.Success.Should().BeTrue();
        var transferId = result.Deliveries!.Should().ContainSingle().Subject.TransferId!.Value;
        var row = await SingleMessageAsync(transferId);
        row.Kind.Should().Be(GiftMessageKind.GiftNote);
        row.Body.Should().Be("For you, enjoy!");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task BulkSendWithAnOverlongNote_SendsNothingAndSaysWhy()
    {
        // Act
        var result = await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { BobPhone },
            new string('a', GiftMessagePolicy.MaxBodyLength + 1));

        // Assert — judged once, before anything is reserved
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("Validation");
        result.ErrorMessage.Should().Contain(GiftMessagePolicy.MaxBodyLength.ToString()).And.Contain("Shorten it");

        var voucher = await ReloadVoucherAsync(_fixture.AliceVoucherId);
        voucher.TransferLockId.Should().BeNull();
        voucher.MemberId.Should().Be(_fixture.AliceMemberId);
        (await _fixture.Context.GiftMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task ThreadOfAGiftThatDoesNotExist_TellsTheMemberToRefreshTheirList()
    {
        // Act
        var read = await _fixture.TransferService.GetThreadAsync(Guid.NewGuid(), _fixture.AliceMemberId);
        var write = await _fixture.TransferService.PostMessageAsync(Guid.NewGuid(), _fixture.AliceMemberId, "Hello?");

        // Assert
        read.Success.Should().BeFalse();
        read.ErrorCode.Should().Be("TransferNotFound");
        read.ErrorMessage.Should().Contain("can't find that gift").And.Contain("Refresh");

        write.Success.Should().BeFalse();
        write.ErrorCode.Should().Be("TransferNotFound");
    }

    // ----- Helpers -----

    private async Task<GiftMessage> SingleMessageAsync(Guid transferId) =>
        await _fixture.Context.GiftMessages.AsNoTracking().SingleAsync(m => m.TransferId == transferId);

    private Task<int> MessageCountAsync(Guid transferId) =>
        _fixture.Context.GiftMessages.AsNoTracking().CountAsync(m => m.TransferId == transferId);

    private async Task<VoucherTransfer> ReloadTransferAsync(Guid transferId)
    {
        var transfer = await _fixture.Context.VoucherTransfers.SingleAsync(t => t.Id == transferId);
        await _fixture.Context.Entry(transfer).ReloadAsync();
        return transfer;
    }

    private async Task<VoucherPlanDetail> ReloadVoucherAsync(Guid voucherId)
    {
        var voucher = await _fixture.Context.VoucherPlanDetails.SingleAsync(v => v.Id == voucherId);
        await _fixture.Context.Entry(voucher).ReloadAsync();
        return voucher;
    }

    /// <summary>Writes follow-ups straight from Alice, spread over the last minutes so they all fall
    /// inside the hourly window the service counts.</summary>
    private async Task SeedMessagesAsync(Guid transferId, int count)
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            _fixture.Context.GiftMessages.Add(GiftMessage.Create(
                transferId,
                _fixture.AliceMemberId,
                _fixture.BobMemberId,
                GiftMessageDirection.FromSender,
                GiftMessageKind.FollowUp,
                $"Seeded message {i + 1}",
                now.AddMinutes(-i - 1)));
        }

        await _fixture.Context.SaveChangesAsync();
    }

    /// <summary>A member who is neither the sender nor the recipient of the gift under test.</summary>
    private async Task<Guid> SeedOutsiderAsync()
    {
        var customer = new Customer
        {
            PhoneNumber = "0909888888",
            FullName = "Carol Outsider",
            Email = "carol@test.com",
            Status = CustomerStatus.Active
        };
        _fixture.Context.Customers.Add(customer);
        await _fixture.Context.SaveChangesAsync();

        var member = new MemberAccount
        {
            CustomerId = customer.Id,
            FullName = "Carol Outsider",
            PasswordHash = "registered-not-a-placeholder",
            Status = MemberAccountStatus.Active
        };
        _fixture.Context.MemberAccounts.Add(member);
        await _fixture.Context.SaveChangesAsync();

        return member.Id;
    }

    public void Dispose() => _fixture.Dispose();
}

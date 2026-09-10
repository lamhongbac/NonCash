using FluentAssertions;
using NonCash.Shared.Helpers;

namespace NonCash.UnitTests.Helpers;

/// <summary>
/// CR-2026-09-10-30 row 7: customers never see the word "transfer", and every refusal tells them
/// both the cause and what to do next.
/// </summary>
public class GiftMessageHelperTests
{
    private static readonly string[] KnownSkipCodes =
    [
        "InvalidPhoneNumber",
        "TransferAlreadyPending",
        "SelfTransferNotAllowed",
        "NotTransferable",
        "NotOwned",
        "VoucherNotFound",
        "RecipientNotFound",
        "RecipientBrandBlocked",
        "RecipientBlacklisted"
    ];

    [Theory]
    [InlineData("PendingAcceptance", "Waiting")]
    [InlineData("Accepted", "Accepted")]
    [InlineData("Rejected", "Declined")]
    [InlineData("Expired", "Expired")]
    [InlineData("Cancelled", "Cancelled by you")]
    public void StatusLabel_UsesCustomerWording(string status, string expected) =>
        GiftMessageHelper.StatusLabel(status).Should().Be(expected);

    [Fact]
    public void StatusLabel_UnknownStatusIsPassedThroughRatherThanBlanked() =>
        GiftMessageHelper.StatusLabel("SomethingNew").Should().Be("SomethingNew");

    [Fact]
    public void IsWaiting_IsTrueOnlyWhileTheRecipientHasNotAnswered()
    {
        GiftMessageHelper.IsWaiting("PendingAcceptance").Should().BeTrue();
        GiftMessageHelper.IsWaiting("Accepted").Should().BeFalse();
        GiftMessageHelper.IsWaiting(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("Emailed", "Accept link emailed")]
    [InlineData("HoldingNoEmail", "Held — no email on file")]
    [InlineData("HoldingNotRegistered", "Held — not on NonCash yet")]
    public void DeliveryLabel_NamesHowTheGiftReachedTheRecipient(string code, string expected) =>
        GiftMessageHelper.DeliveryLabel(code).Should().Be(expected);

    [Fact]
    public void OnItsWayBadge_IsTheWalletWordingForAReservedVoucher() =>
        GiftMessageHelper.OnItsWayBadge.Should().Be("On its way");

    [Fact]
    public void SendGiftButtonLabel_IsTheShortWordingTheProductOwnerChose() =>
        GiftMessageHelper.SendGiftButtonLabel.Should().Be("Gửi Quà");

    [Theory]
    [InlineData("GiftNote", "Sent with the gift")]
    [InlineData("ThankYou", "Thank-you")]
    [InlineData("DeclineReason", "Decline reason")]
    [InlineData("FollowUp", "Message")]
    public void KindLabel_NamesEachLineOfAConversation(string kind, string expected) =>
        GiftMessageHelper.KindLabel(kind).Should().Be(expected);

    [Fact]
    public void KindLabel_UnknownKindGetsNoLabelRatherThanAWrongOne()
    {
        GiftMessageHelper.KindLabel("SomethingNew").Should().BeEmpty();
        GiftMessageHelper.KindLabel(null).Should().BeEmpty();
    }

    [Fact]
    public void BrandPrivacyNote_TellsTheBrandWhatIsHiddenFromThem() =>
        GiftMessageHelper.BrandPrivacyNote.Should().Contain("follow-up").And.Contain("stay between them");

    [Fact]
    public void DescribeSkip_AlwaysNamesThePhoneAndGivesANextAction()
    {
        foreach (var code in KnownSkipCodes)
        {
            var message = GiftMessageHelper.DescribeSkip("0909123456", code);

            message.Should().Contain("0909123456", $"the {code} message must say which recipient it is about");
            message.Should().Contain(".", $"the {code} message must explain the cause");
            message.Should().NotBe(code);
            message.ToLowerInvariant().Should().NotContain("transfer", $"the {code} message must stay customer-friendly");
        }
    }

    [Fact]
    public void DescribeSkip_AlreadyOnItsWay_PointsAtTheCancelRoute() =>
        GiftMessageHelper.DescribeSkip("0909123456", "TransferAlreadyPending")
            .Should().Contain("on its way")
            .And.Contain("Gifts you sent");

    [Fact]
    public void DescribeSkip_OwnNumber_TellsTheSenderToUseAFriendsNumber() =>
        GiftMessageHelper.DescribeSkip("0909123456", "SelfTransferNotAllowed")
            .Should().Contain("your own number");

    [Fact]
    public void DescribeSkip_UnknownCode_StillExplainsAndTellsThemToRetry() =>
        GiftMessageHelper.DescribeSkip("0909123456", "SomeNewCode")
            .Should().Contain("SomeNewCode")
            .And.Contain("Refresh My Vouchers");
}

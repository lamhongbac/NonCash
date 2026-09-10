using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Repositories;
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.Transfers;

/// <summary>
/// CR-2026-09-10-30: gifting with confirmation and a trail. Sending no longer moves a voucher —
/// it reserves it, records it in voucher_transfers and tells the sender honestly how it was
/// delivered. Ownership changes only when the recipient accepts, and the sender is told the
/// outcome (thank-you note, decline reason or expiry) either way.
/// </summary>
public class GiftConfirmationFlowTests
{
    private const string BobPhone = "0909222222";
    private const string StrangerPhone = "0999777666";
    private const string ThankYou = "Cảm ơn món quà của bạn!";

    private readonly TransferAcceptanceTestFixture _fixture;

    public GiftConfirmationFlowTests()
    {
        _fixture = new TransferAcceptanceTestFixture();
    }

    /// <summary>The wallet's "Send a gift" button posts here: one voucher per phone number.</summary>
    private TransferService BulkSender() => new(
        _fixture.VoucherRepository,
        _fixture.CustomerRepository,
        _fixture.MemberRepository,
        new Repository<VoucherDistribution>(_fixture.Context),
        _fixture.TransferService);

    // ----- Row 1 + 2: sending reserves the voucher and reports the truth -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task Send_ToRegisteredFriendWithEmail_ReservesVoucherAndReportsEmailed()
    {
        // Act
        var result = await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { BobPhone });

        // Assert — the sender is told an accept link went out
        result.Success.Should().BeTrue();
        result.TransferredCount.Should().Be(1);
        var delivery = result.Deliveries!.Should().ContainSingle().Subject;
        delivery.DeliveryStatus.Should().Be(GiftDeliveryStatus.Emailed);
        delivery.RecipientPhone.Should().Be(BobPhone);
        delivery.Message.Should().Contain("We emailed").And.Contain("secure link");

        // The voucher stays in Alice's wallet, reserved, and no ownership record is written yet
        var voucher = await ReloadVoucherAsync(_fixture.AliceVoucherId);
        voucher.MemberId.Should().Be(_fixture.AliceMemberId);
        voucher.TransferLockId.Should().NotBeNull();
        (await _fixture.Context.VoucherDistributions.CountAsync()).Should().Be(0);

        // The friend got the accept link by email
        var sent = _fixture.Notifications.GiftsSent.Should().ContainSingle().Subject;
        sent.RecipientEmail.Should().Be("bob@test.com");
        sent.SenderName.Should().Be("Alice Sender");
        sent.MagicLinkUrl.Should().Contain("/member/welcome?token=");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task Send_ToPhoneNotOnNonCash_HoldsGiftAndAsksSenderToInviteThem()
    {
        // Act
        var result = await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { StrangerPhone });

        // Assert — nothing was emailed, and the sender is told exactly what to do next
        result.Success.Should().BeTrue();
        var delivery = result.Deliveries!.Should().ContainSingle().Subject;
        delivery.DeliveryStatus.Should().Be(GiftDeliveryStatus.HoldingNotRegistered);
        delivery.Message.Should().Contain("isn't on NonCash yet").And.Contain("register");
        _fixture.Notifications.GiftsSent.Should().BeEmpty();

        // A placeholder wallet was opened so the gift can be handed over once they register
        var placeholder = await FindMemberByPhoneAsync(StrangerPhone);
        placeholder.PasswordHash.Should().BeEmpty();

        var voucher = await ReloadVoucherAsync(_fixture.AliceVoucherId);
        voucher.MemberId.Should().Be(_fixture.AliceMemberId);
        voucher.TransferLockId.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task Send_ToFriendWithoutEmail_HoldsGiftAndTellsSenderToAskThemToSignIn()
    {
        // Arrange — a registered member whose customer profile has no email address
        await SeedMemberWithoutEmailAsync("0909444444", "Carol No Email");

        // Act
        var result = await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { "0909444444" });

        // Assert
        result.Success.Should().BeTrue();
        var delivery = result.Deliveries!.Should().ContainSingle().Subject;
        delivery.DeliveryStatus.Should().Be(GiftDeliveryStatus.HoldingNoEmail);
        delivery.Message.Should().Contain("has no email on file").And.Contain("sign in");
        _fixture.Notifications.GiftsSent.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task Send_ToAutoProvisionedFriendWithEmail_EmailsTheAcceptLinkEvenWithoutAPassword()
    {
        // Arrange — the state a brand's promotion leaves behind: a customer with an email but no password.
        // The magic link exists so exactly this member can sign in, so the gift must still be emailed.
        await SeedAutoProvisionedMemberAsync("0909555555", "Danh Auto Provisioned", "danh@test.com");

        // Act
        var result = await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { "0909555555" });

        // Assert
        result.Success.Should().BeTrue();
        var delivery = result.Deliveries!.Should().ContainSingle().Subject;
        delivery.DeliveryStatus.Should().Be(GiftDeliveryStatus.Emailed);
        delivery.Message.Should().Contain("We emailed").And.NotContain("isn't on NonCash yet");

        var sent = _fixture.Notifications.GiftsSent.Should().ContainSingle().Subject;
        sent.RecipientEmail.Should().Be("danh@test.com");
        sent.MagicLinkUrl.Should().Contain("/member/welcome?token=");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task Send_WithUnusablePhoneNumber_SkipsItAndReservesNothing()
    {
        // Act
        var result = await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { "not-a-phone" });

        // Assert — the sender gets the reason per recipient, and the voucher is untouched
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NoEligibleRecipients");
        result.SkippedRecords.Should().ContainSingle()
            .Which.Reason.Should().Be("InvalidPhoneNumber");

        var voucher = await ReloadVoucherAsync(_fixture.AliceVoucherId);
        voucher.TransferLockId.Should().BeNull();
        voucher.MemberId.Should().Be(_fixture.AliceMemberId);
    }

    // ----- Row 4: the recipient confirms and thanks the sender -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task Accept_WithThankYouNote_MovesVoucherRecordsGiftAndTellsSender()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "For you");

        // Act
        var accepted = await _fixture.TransferService.AcceptAsync(
            initiated.TransferId!.Value, _fixture.BobMemberId, ThankYou);

        // Assert — ownership and the gift log only change now
        accepted.Success.Should().BeTrue();
        var voucher = await ReloadVoucherAsync(_fixture.AliceVoucherId);
        voucher.MemberId.Should().Be(_fixture.BobMemberId);
        voucher.TransferLockId.Should().BeNull();

        var distribution = await _fixture.Context.VoucherDistributions.SingleAsync();
        distribution.VoucherId.Should().Be(_fixture.AliceVoucherId);
        distribution.MemberId.Should().Be(_fixture.BobMemberId);
        distribution.Method.Should().Be(DistributionMethod.Transfer);

        var transfer = await ReloadTransferAsync(initiated.TransferId.Value);
        transfer.Status.Should().Be(VoucherTransferStatus.Accepted);
        transfer.RecipientNote.Should().Be(ThankYou);
        transfer.RespondedAt.Should().NotBeNull();

        // Accepting a brand's voucher makes the recipient that brand's customer
        var link = await _fixture.Context.BrandCustomers.SingleAsync(bc => bc.CustomerId == _fixture.BobCustomerId);
        link.BrandId.Should().Be(_fixture.BrandId);
        link.Source.Should().Be(BrandCustomerSource.GiftingAuto);

        // The sender hears back, with the thank-you note
        var told = _fixture.Notifications.GiftsAccepted.Should().ContainSingle().Subject;
        told.SenderEmail.Should().Be("alice@test.com");
        told.RecipientName.Should().Be("Bob Receiver");
        told.RecipientThankYouNote.Should().Be(ThankYou);
        told.FaceValue.Should().Be(100000m);
        told.VoucherName.Should().Be("Test Coffee voucher");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task Decline_WithReason_LeavesVoucherWithSenderAndTellsThemWhy()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);

        // Act
        var declined = await _fixture.TransferService.RejectAsync(
            initiated.TransferId!.Value, _fixture.BobMemberId, "I already have one");

        // Assert
        declined.Success.Should().BeTrue();
        var voucher = await ReloadVoucherAsync(_fixture.AliceVoucherId);
        voucher.MemberId.Should().Be(_fixture.AliceMemberId);
        voucher.TransferLockId.Should().BeNull();
        (await _fixture.Context.VoucherDistributions.CountAsync()).Should().Be(0);

        var told = _fixture.Notifications.GiftsDeclined.Should().ContainSingle().Subject;
        told.SenderEmail.Should().Be("alice@test.com");
        told.Reason.Should().Be("I already have one");
        _fixture.Notifications.GiftsAccepted.Should().BeEmpty();
    }

    // ----- Row 4 + 5: an unanswered gift comes home and the sender is told -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task ExpirySweep_ReleasesVoucherAndTellsSender()
    {
        // Arrange
        var initiated = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);
        var transfer = await _fixture.Context.VoucherTransfers.SingleAsync(t => t.Id == initiated.TransferId);
        transfer.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await _fixture.Context.SaveChangesAsync();

        // Act
        var swept = await _fixture.TransferService.SweepExpiredAsync(DateTime.UtcNow);

        // Assert
        swept.Should().Be(1);
        var expired = await ReloadTransferAsync(initiated.TransferId!.Value);
        expired.Status.Should().Be(VoucherTransferStatus.Expired);

        var voucher = await ReloadVoucherAsync(_fixture.AliceVoucherId);
        voucher.MemberId.Should().Be(_fixture.AliceMemberId);
        voucher.TransferLockId.Should().BeNull();

        var told = _fixture.Notifications.GiftsExpired.Should().ContainSingle().Subject;
        told.SenderEmail.Should().Be("alice@test.com");
        told.RecipientPhone.Should().Be(BobPhone);
        told.ExpiryDays.Should().Be(7);
    }

    // ----- Row 3: a gift held for a stranger is emailed the moment they register -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task HeldGift_IsEmailedWithAcceptLinkWhenRecipientRegisters()
    {
        // Arrange — a gift held for a phone that is not on NonCash yet
        await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { StrangerPhone });
        _fixture.Notifications.GiftsSent.Should().BeEmpty();

        var placeholder = await FindMemberByPhoneAsync(StrangerPhone);

        // Act — registration fills in the email and the password the placeholder lacked
        var customer = await _fixture.Context.Customers.SingleAsync(c => c.Id == placeholder.CustomerId);
        customer.Email = "stranger@test.com";
        placeholder.PasswordHash = "registered-not-a-placeholder";
        await _fixture.Context.SaveChangesAsync();

        await _fixture.TransferService.NotifyPendingGiftsAsync(placeholder.Id);

        // Assert
        var sent = _fixture.Notifications.GiftsSent.Should().ContainSingle().Subject;
        sent.RecipientEmail.Should().Be("stranger@test.com");
        sent.MagicLinkUrl.Should().Contain("/member/welcome?token=");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task HeldGift_IsNotEmailedWhileRecipientIsStillAPlaceholder()
    {
        // Arrange
        await BulkSender().TransferAsync(
            _fixture.AliceMemberId,
            new[] { _fixture.AliceVoucherId },
            new[] { StrangerPhone });
        var placeholder = await FindMemberByPhoneAsync(StrangerPhone);

        // Act
        await _fixture.TransferService.NotifyPendingGiftsAsync(placeholder.Id);

        // Assert — no address to write to yet, so nothing is sent
        _fixture.Notifications.GiftsSent.Should().BeEmpty();
    }

    // ----- Row 6: the brand's read-only gift log -----

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task BrandGiftLog_ShowsWhoSentWhatToWhom_ForThatBrandOnly()
    {
        // Arrange
        await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, "Happy birthday");

        // Act
        var ownBrand = await _fixture.TransferService.GetBrandGiftsAsync(_fixture.BrandId);
        var otherBrand = await _fixture.TransferService.GetBrandGiftsAsync(Guid.NewGuid());

        // Assert
        var row = ownBrand.Should().ContainSingle().Subject;
        row.SerialNo.Should().Be("VC-TEST-00000001");
        row.BrandName.Should().Be("Test Coffee");
        row.SenderName.Should().Be("Alice Sender");
        row.SenderPhone.Should().Be("0909111111");
        row.RecipientName.Should().Be("Bob Receiver");
        row.RecipientPhone.Should().Be(BobPhone);
        row.Note.Should().Be("Happy birthday");
        row.Status.Should().Be(VoucherTransferStatus.PendingAcceptance);
        row.RespondedAt.Should().BeNull();

        otherBrand.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-30")]
    public async Task BrandGiftLog_CanBeFilteredByStatus()
    {
        // Arrange — one waiting gift and one accepted gift
        var waiting = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceMemberId, _fixture.AliceVoucherId, BobPhone, null, null);
        await _fixture.TransferService.AcceptAsync(waiting.TransferId!.Value, _fixture.BobMemberId, ThankYou);

        // Act
        var accepted = await _fixture.TransferService.GetBrandGiftsAsync(
            _fixture.BrandId, VoucherTransferStatus.Accepted);
        var stillWaiting = await _fixture.TransferService.GetBrandGiftsAsync(
            _fixture.BrandId, VoucherTransferStatus.PendingAcceptance);

        // Assert
        var row = accepted.Should().ContainSingle().Subject;
        row.RecipientNote.Should().Be(ThankYou);
        row.RespondedAt.Should().NotBeNull();
        stillWaiting.Should().BeEmpty();
    }

    // ----- Helpers -----

    private async Task<VoucherPlanDetail> ReloadVoucherAsync(Guid voucherId)
    {
        var voucher = await _fixture.Context.VoucherPlanDetails.SingleAsync(v => v.Id == voucherId);
        await _fixture.Context.Entry(voucher).ReloadAsync();
        return voucher;
    }

    private async Task<VoucherTransfer> ReloadTransferAsync(Guid transferId)
    {
        var transfer = await _fixture.Context.VoucherTransfers.SingleAsync(t => t.Id == transferId);
        await _fixture.Context.Entry(transfer).ReloadAsync();
        return transfer;
    }

    private async Task<MemberAccount> FindMemberByPhoneAsync(string phone)
    {
        var customer = await _fixture.Context.Customers.SingleAsync(c => c.PhoneNumber == phone);
        return await _fixture.Context.MemberAccounts.SingleAsync(m => m.CustomerId == customer.Id);
    }

    private async Task SeedMemberWithoutEmailAsync(string phone, string fullName)
    {
        var customer = new Customer
        {
            PhoneNumber = phone,
            FullName = fullName,
            Status = CustomerStatus.Active
        };
        _fixture.Context.Customers.Add(customer);
        await _fixture.Context.SaveChangesAsync();

        _fixture.Context.MemberAccounts.Add(new MemberAccount
        {
            CustomerId = customer.Id,
            FullName = fullName,
            PasswordHash = "registered-not-a-placeholder",
            Status = MemberAccountStatus.Active
        });
        await _fixture.Context.SaveChangesAsync();
    }

    private async Task SeedAutoProvisionedMemberAsync(string phone, string fullName, string email)
    {
        var customer = new Customer
        {
            PhoneNumber = phone,
            FullName = fullName,
            Email = email,
            Status = CustomerStatus.Active
        };
        _fixture.Context.Customers.Add(customer);
        await _fixture.Context.SaveChangesAsync();

        // Empty PasswordHash is exactly what promotion/gifting provisioning writes.
        _fixture.Context.MemberAccounts.Add(new MemberAccount
        {
            CustomerId = customer.Id,
            FullName = fullName,
            PasswordHash = string.Empty,
            Status = MemberAccountStatus.Active
        });
        await _fixture.Context.SaveChangesAsync();
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.Transfers;

/// <summary>
/// User Acceptance Tests for the peer-to-peer voucher transfer feature (Epic 5).
/// These tests verify the complete transfer lifecycle through the service layer.
/// </summary>
public class VoucherTransferServiceAcceptanceTests
{
    private readonly TransferAcceptanceTestFixture _fixture;

    public VoucherTransferServiceAcceptanceTests()
    {
        // Fresh fixture per test to ensure isolation
        _fixture = new TransferAcceptanceTestFixture();
    }

    #region Story 5-1: Initiate Transfer

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-1")]
    public async Task InitiateTransfer_WithValidRecipientPhone_CreatesPendingTransfer()
    {
        // Act
        var result = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Happy birthday!");

        // Assert
        result.Success.Should().BeTrue();
        result.TransferId.Should().NotBeNull();

        var transfer = await _fixture.Context.VoucherTransfers.FindAsync(result.TransferId);
        transfer.Should().NotBeNull();
        transfer!.Status.Should().Be(VoucherTransferStatus.PendingAcceptance);
        transfer.SenderId.Should().Be(_fixture.AliceUserId);
        transfer.RecipientId.Should().Be(_fixture.BobUserId);
        transfer.VoucherId.Should().Be(_fixture.AliceVoucherId);

        var voucher = await _fixture.Context.VoucherPlanDetails.FindAsync(_fixture.AliceVoucherId);
        voucher!.TransferLockId.Should().Be(transfer.Id);
        voucher.TransferLockedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-1")]
    public async Task InitiateTransfer_WithVoucherNotOwnedBySender_ReturnsNotOwnedError()
    {
        // Act - Alice tries to transfer Bob's voucher
        var result = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.BobVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "This is not my voucher");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NotOwned");
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-1")]
    public async Task InitiateTransfer_WithRecipientNotFound_CreatesPlaceholderUserAndTransfer()
    {
        // Act
        var result = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0999999999",
            recipientMemberId: null,
            note: "Unknown recipient");

        // Assert - system creates placeholder customer/user for new phone
        result.Success.Should().BeTrue();
        result.TransferId.Should().NotBeNull();

        var transfer = await _fixture.Context.VoucherTransfers.FindAsync(result.TransferId);
        transfer.Should().NotBeNull();
        transfer!.SenderId.Should().Be(_fixture.AliceUserId);
        transfer.RecipientId.Should().NotBe(_fixture.AliceUserId);
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-1")]
    public async Task InitiateTransfer_WithPendingTransferAlreadyExists_ReturnsTransferAlreadyPendingError()
    {
        // Arrange
        await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "First transfer");

        // Act
        var result = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Second transfer");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("TransferAlreadyPending");
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-1")]
    public async Task InitiateTransfer_ToSelf_ReturnsSelfTransferNotAllowedError()
    {
        // Act - Alice tries to send to her own phone
        var result = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909111111",
            recipientMemberId: null,
            note: "To myself");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SelfTransferNotAllowed");
    }

    #endregion

    #region Story 5-2: Recipient Confirmation

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-2")]
    public async Task AcceptTransfer_ByRecipient_TransfersVoucherOwnership()
    {
        // Arrange
        var initiateResult = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Accept me");

        // Act
        var acceptResult = await _fixture.TransferService.AcceptAsync(
            initiateResult.TransferId!.Value,
            _fixture.BobUserId);

        // Assert
        acceptResult.Success.Should().BeTrue();
        acceptResult.Status.Should().Be("Accepted");
        acceptResult.VoucherId.Should().Be(_fixture.AliceVoucherId);

        var voucher = await _fixture.Context.VoucherPlanDetails.FindAsync(_fixture.AliceVoucherId);
        await _fixture.Context.Entry(voucher!).ReloadAsync();
        voucher!.MemberId.Should().Be(_fixture.BobUserId);
        voucher.TransferLockId.Should().BeNull();
        voucher.TransferLockedAt.Should().BeNull();

        var transfer = await _fixture.Context.VoucherTransfers.FindAsync(initiateResult.TransferId.Value);
        await _fixture.Context.Entry(transfer!).ReloadAsync();
        transfer!.Status.Should().Be(VoucherTransferStatus.Accepted);
        transfer.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-2")]
    public async Task RejectTransfer_ByRecipient_ReleasesVoucherLock()
    {
        // Arrange
        var initiateResult = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Reject me");

        // Act
        var rejectResult = await _fixture.TransferService.RejectAsync(
            initiateResult.TransferId!.Value,
            _fixture.BobUserId,
            reason: "Changed my mind");

        // Assert
        rejectResult.Success.Should().BeTrue();
        rejectResult.Status.Should().Be("Rejected");

        var voucher = await _fixture.Context.VoucherPlanDetails.FindAsync(_fixture.AliceVoucherId);
        await _fixture.Context.Entry(voucher!).ReloadAsync();
        voucher!.MemberId.Should().Be(_fixture.AliceUserId); // ownership unchanged
        voucher.TransferLockId.Should().BeNull();
        voucher.TransferLockedAt.Should().BeNull();

        var transfer = await _fixture.Context.VoucherTransfers.FindAsync(initiateResult.TransferId.Value);
        await _fixture.Context.Entry(transfer!).ReloadAsync();
        transfer!.Status.Should().Be(VoucherTransferStatus.Rejected);
        transfer.RejectReason.Should().Be("Changed my mind");
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-2")]
    public async Task AcceptTransfer_ByNonRecipient_ReturnsForbiddenError()
    {
        // Arrange
        var initiateResult = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Accept me");

        // Act - Alice tries to accept her own transfer
        var acceptResult = await _fixture.TransferService.AcceptAsync(
            initiateResult.TransferId!.Value,
            _fixture.AliceUserId);

        // Assert
        acceptResult.Success.Should().BeFalse();
        acceptResult.ErrorCode.Should().Be("Forbidden");
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-2")]
    public async Task AcceptTransfer_AfterAlreadyAccepted_ReturnsAlreadyResolvedError()
    {
        // Arrange
        var initiateResult = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Accept me");

        await _fixture.TransferService.AcceptAsync(initiateResult.TransferId!.Value, _fixture.BobUserId);

        // Act - second accept
        var acceptResult = await _fixture.TransferService.AcceptAsync(
            initiateResult.TransferId.Value,
            _fixture.BobUserId);

        // Assert
        acceptResult.Success.Should().BeFalse();
        acceptResult.ErrorCode.Should().Be("AlreadyResolved");
    }

    #endregion

    #region Story 5-3: Sender Cancel & History

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-3")]
    public async Task CancelTransfer_BySender_BeforeRecipientAction_ReleasesLock()
    {
        // Arrange
        var initiateResult = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Cancel me");

        // Act
        var cancelResult = await _fixture.TransferService.CancelAsync(
            initiateResult.TransferId!.Value,
            _fixture.AliceUserId);

        // Assert
        cancelResult.Success.Should().BeTrue();
        cancelResult.Status.Should().Be("Cancelled");

        var voucher = await _fixture.Context.VoucherPlanDetails.FindAsync(_fixture.AliceVoucherId);
        await _fixture.Context.Entry(voucher!).ReloadAsync();
        voucher!.MemberId.Should().Be(_fixture.AliceUserId);
        voucher.TransferLockId.Should().BeNull();

        var transfer = await _fixture.Context.VoucherTransfers.FindAsync(initiateResult.TransferId.Value);
        await _fixture.Context.Entry(transfer!).ReloadAsync();
        transfer!.Status.Should().Be(VoucherTransferStatus.Cancelled);
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-3")]
    public async Task CancelTransfer_ByNonSender_ReturnsForbiddenError()
    {
        // Arrange
        var initiateResult = await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Cancel me");

        // Act - Bob tries to cancel Alice's transfer
        var cancelResult = await _fixture.TransferService.CancelAsync(
            initiateResult.TransferId!.Value,
            _fixture.BobUserId);

        // Assert
        cancelResult.Success.Should().BeFalse();
        cancelResult.ErrorCode.Should().Be("Forbidden");
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-3")]
    public async Task GetInbox_ReturnsPendingTransfersForRecipient()
    {
        // Arrange
        await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Inbox test");

        // Act
        var inbox = await _fixture.TransferService.GetInboxAsync(_fixture.BobUserId);

        // Assert
        inbox.Should().ContainSingle();
        inbox[0].SenderId.Should().Be(_fixture.AliceUserId);
        inbox[0].Status.Should().Be(VoucherTransferStatus.PendingAcceptance);
    }

    [Fact]
    [Trait("Category", "UAT")]
    [Trait("Story", "5-3")]
    public async Task GetOutbox_ReturnsPendingTransfersForSender()
    {
        // Arrange
        await _fixture.TransferService.InitiateAsync(
            _fixture.AliceUserId,
            _fixture.AliceVoucherId,
            recipientPhone: "0909222222",
            recipientMemberId: null,
            note: "Outbox test");

        // Act
        var outbox = await _fixture.TransferService.GetOutboxAsync(_fixture.AliceUserId);

        // Assert
        outbox.Should().ContainSingle();
        outbox[0].RecipientId.Should().Be(_fixture.BobUserId);
        outbox[0].Status.Should().Be(VoucherTransferStatus.PendingAcceptance);
    }

    #endregion
}

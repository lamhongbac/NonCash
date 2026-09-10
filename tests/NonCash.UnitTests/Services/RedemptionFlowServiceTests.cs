using FluentAssertions;
using NSubstitute;
using NonCash.Pos.Models;
using NonCash.Pos.Services;

namespace NonCash.UnitTests.Services;

/// <summary>
/// CR-25: one-click redemption is client-side orchestration of the unchanged LOCK + COMMIT
/// contract, so the guarantees that matter are the ones a cashier depends on — a rejected
/// lock never leaves a dangling lock, and a failed commit keeps the lock visible so it can
/// be committed or rolled back deliberately.
/// </summary>
public class RedemptionFlowServiceTests
{
    private const string Code = "voucher-code";
    private static readonly Guid OutletId = Guid.NewGuid();
    private static readonly Guid LockId = Guid.NewGuid();

    private readonly IPosApiClient _api = Substitute.For<IPosApiClient>();
    private readonly RedemptionFlowService _sut;

    public RedemptionFlowServiceTests()
    {
        _sut = new RedemptionFlowService(_api);
    }

    private static PosVoucherInfo Voucher(decimal faceValue = 100_000m) => new(
        faceValue, "Cash", DateTime.UtcNow.AddDays(10), "Test Brand", "VC-TEST-1", "All stores", new List<string>());

    private void GivenLockSucceeds(PosVoucherInfo? info) =>
        _api.LockAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new PosLockResponse("Locked", null, LockId, info));

    [Fact]
    public async Task Redeem_LockAndCommitSucceed_CommitsWithFaceValueWhenAmountNotTyped()
    {
        GivenLockSucceeds(Voucher(250_000m));
        _api.CommitAsync(LockId, Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new PosCommitResponse("Success", null, null));

        var outcome = await _sut.RedeemAsync(Code, OutletId, "BILL-1", "TXN-1", amountUsed: null);

        outcome.Committed.Should().BeTrue();
        outcome.LockId.Should().BeNull();
        outcome.AmountUsed.Should().Be(250_000m);
        await _api.Received(1).CommitAsync(LockId, "TXN-1", 250_000m, Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Redeem_WithTypedAmount_CommitsTheTypedAmount()
    {
        GivenLockSucceeds(Voucher(250_000m));
        _api.CommitAsync(LockId, Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new PosCommitResponse("Success", null, null));

        var outcome = await _sut.RedeemAsync(Code, OutletId, "BILL-1", "TXN-1", amountUsed: 80_000m);

        outcome.Committed.Should().BeTrue();
        outcome.AmountUsed.Should().Be(80_000m);
    }

    [Fact]
    public async Task Redeem_LockRejected_DoesNotCommitAndReportsReason()
    {
        _api.LockAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new PosLockResponse("Invalid", PosFailureReasons.OutletNotAuthorized, null, null));

        var outcome = await _sut.RedeemAsync(Code, OutletId, "BILL-1", "TXN-1", amountUsed: null);

        outcome.Committed.Should().BeFalse();
        outcome.Reason.Should().Be(PosFailureReasons.OutletNotAuthorized);
        outcome.LockId.Should().BeNull();
        await _api.DidNotReceiveWithAnyArgs().CommitAsync(default, Arg.Any<string>(), default, Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Redeem_CommitFails_KeepsTheLockForManualFollowUp()
    {
        GivenLockSucceeds(Voucher());
        _api.CommitAsync(LockId, Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new PosCommitResponse("Failed", PosFailureReasons.AlreadyComplete, "Already completed."));

        var outcome = await _sut.RedeemAsync(Code, OutletId, "BILL-1", "TXN-1", amountUsed: null);

        outcome.Committed.Should().BeFalse();
        outcome.LockId.Should().Be(LockId);
        outcome.Reason.Should().Be(PosFailureReasons.AlreadyComplete);
        await _api.DidNotReceiveWithAnyArgs().RollbackAsync(default);
    }

    [Fact]
    public async Task Redeem_NoServerResponseOnLock_StatesTheVoucherWasNotRedeemed()
    {
        _api.LockAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns((PosLockResponse?)null);

        var outcome = await _sut.RedeemAsync(Code, OutletId, "BILL-1", "TXN-1", amountUsed: null);

        outcome.Committed.Should().BeFalse();
        outcome.Message.Should().Contain("not redeemed");
        await _api.DidNotReceiveWithAnyArgs().CommitAsync(default, Arg.Any<string>(), default, Arg.Any<string?>(), Arg.Any<string?>());
    }
}

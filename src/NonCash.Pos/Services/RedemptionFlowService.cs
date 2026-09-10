using NonCash.Pos.Models;

namespace NonCash.Pos.Services;

/// <summary>Result of a one-click redemption attempt.</summary>
/// <param name="Committed">True when the voucher is fully redeemed.</param>
/// <param name="LockId">Non-null when the voucher was locked but the commit did not succeed —
/// the lock is still held server-side and must be committed or rolled back.</param>
/// <param name="Reason">Raw API reason code, when the API returned one.</param>
/// <param name="Message">Fallback message when the API returned no reason.</param>
/// <param name="VoucherInfo">Voucher details returned by the lock call, if any.</param>
/// <param name="AmountUsed">Amount actually sent to commit.</param>
public sealed record RedemptionOutcome(
    bool Committed,
    Guid? LockId,
    string? Reason,
    string? Message,
    PosVoucherInfo? VoucherInfo,
    decimal AmountUsed);

/// <summary>
/// One-click redemption: LOCK then COMMIT against the unchanged public POS contract.
/// The server re-validates the voucher on lock, so no separate verify call is needed.
/// </summary>
public class RedemptionFlowService
{
    private readonly IPosApiClient _api;

    public RedemptionFlowService(IPosApiClient api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <param name="amountUsed">Amount to consume; null means the voucher's full face value.</param>
    public async Task<RedemptionOutcome> RedeemAsync(
        string voucherCode,
        Guid outletId,
        string billNumber,
        string transactionId,
        decimal? amountUsed,
        string? posNo = null,
        string? operatorId = null)
    {
        var lockResult = await _api.LockAsync(voucherCode, outletId, billNumber, posNo, operatorId);

        if (lockResult is null)
        {
            return new RedemptionOutcome(false, null, null,
                "No response from server while locking the voucher. The voucher was not redeemed — verify the code again.",
                null, 0m);
        }

        if (lockResult.Status != "Locked" || lockResult.LockId is null)
        {
            return new RedemptionOutcome(false, null, lockResult.Reason,
                lockResult.Reason is null ? "Lock failed." : null,
                lockResult.VoucherInfo, 0m);
        }

        var amount = amountUsed is > 0 ? amountUsed.Value : lockResult.VoucherInfo?.FaceValue ?? 0m;

        var commitResult = await _api.CommitAsync(lockResult.LockId.Value, transactionId, amount, posNo, operatorId);

        if (commitResult?.Status == "Success")
        {
            return new RedemptionOutcome(true, null, null, commitResult.Message, lockResult.VoucherInfo, amount);
        }

        // Commit failed while the lock is held. Release it here would hand the voucher back
        // silently, so the lock is surfaced and the cashier commits or rolls back explicitly.
        return new RedemptionOutcome(false, lockResult.LockId, commitResult?.Reason,
            commitResult?.Message ?? "Commit failed. The voucher is still locked — press Commit to retry or Rollback to release it.",
            lockResult.VoucherInfo, amount);
    }
}

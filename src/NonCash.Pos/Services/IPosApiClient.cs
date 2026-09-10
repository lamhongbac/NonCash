using NonCash.Pos.Models;

namespace NonCash.Pos.Services;

/// <summary>POS integration contract as consumed by this client (X-API-Key endpoints).</summary>
public interface IPosApiClient
{
    Task<PosVerifyResponse?> VerifyAsync(string voucherCode, Guid outletId);
    Task<PosLockResponse?> LockAsync(string voucherCode, Guid outletId, string billNumber, string? posNo = null, string? operatorId = null);
    Task<PosCommitResponse?> CommitAsync(Guid lockId, string transactionId, decimal amountUsed, string? posNo = null, string? operatorId = null);
    Task<PosRollbackResponse?> RollbackAsync(Guid lockId);
}

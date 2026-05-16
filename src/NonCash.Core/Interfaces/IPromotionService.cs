using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IPromotionService
{
    Task<PromotionResult> DistributeAsync(
        Guid planId,
        Guid brandId,
        IReadOnlyList<string> phoneNumbers,
        CancellationToken cancellationToken = default);
}

public record PromotionResult(
    bool Success,
    int DistributedCount = 0,
    int SkippedCount = 0,
    IReadOnlyList<SkippedRecord>? SkippedRecords = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record SkippedRecord(string PhoneNumber, string Reason);

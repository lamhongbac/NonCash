using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Maker-checker credit adjustment workflow (Epic 10).
/// Requests below the approval bar auto-apply; the rest wait for a FinancialController.
/// Self-approval is rejected here regardless of the caller's role.
/// </summary>
public class CreditAdjustmentService : ICreditAdjustmentService
{
    private static readonly CreditBatchType[] AlwaysApprovalTypes =
    {
        CreditBatchType.Correction, CreditBatchType.Clawback, CreditBatchType.Reinstatement
    };

    private static readonly CreditBatchType[] ThresholdApprovalTypes =
    {
        CreditBatchType.Grant, CreditBatchType.Compensation
    };

    private readonly ApplicationDbContext _db;
    private readonly ICreditService _creditService;
    private readonly ICreditPolicyService _policyService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CreditAdjustmentService> _logger;

    public CreditAdjustmentService(
        ApplicationDbContext db,
        ICreditService creditService,
        ICreditPolicyService policyService,
        INotificationService notificationService,
        ILogger<CreditAdjustmentService> logger)
    {
        _db = db;
        _creditService = creditService;
        _policyService = policyService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<CreditAdjustmentRequest> RequestAsync(CreditAdjustmentCommand command, CancellationToken cancellationToken = default)
    {
        if (!AlwaysApprovalTypes.Contains(command.AdjustmentType) && !ThresholdApprovalTypes.Contains(command.AdjustmentType))
            throw new InvalidOperationException($"{command.AdjustmentType} is not a valid adjustment type. Use the purchase top-up flow for Purchase.");
        if (command.Amount <= 0)
            throw new InvalidOperationException("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(command.ReasonText))
            throw new InvalidOperationException("A reason is required for every adjustment.");

        if (AlwaysApprovalTypes.Contains(command.AdjustmentType))
        {
            if (command.RelatedBatchId is null)
                throw new InvalidOperationException($"{command.AdjustmentType} requires the related batch being fixed.");

            var relatedBatchOk = await _db.CreditBatches
                .AsNoTracking()
                .AnyAsync(b => b.Id == command.RelatedBatchId && b.BrandId == command.BrandId, cancellationToken);
            if (!relatedBatchOk)
                throw new InvalidOperationException("Related batch not found for this brand.");
        }

        var policy = await _policyService.ResolveForBrandAsync(command.BrandId, cancellationToken);

        // Approval matrix: Correction/Clawback/Reinstatement always; Grant/Compensation
        // at/above the threshold (no threshold configured = always).
        var requiresApproval = AlwaysApprovalTypes.Contains(command.AdjustmentType)
            || policy.AdjustmentApprovalThreshold is null
            || command.Amount >= policy.AdjustmentApprovalThreshold.Value;

        var request = new CreditAdjustmentRequest
        {
            BrandId = command.BrandId,
            AdjustmentType = command.AdjustmentType,
            Amount = command.Amount,
            RelatedBatchId = command.RelatedBatchId,
            ReasonText = command.ReasonText.Trim(),
            EvidenceNote = command.EvidenceNote,
            EvidenceImageUrl = command.EvidenceImageUrl,
            Status = AdjustmentStatus.PendingApproval,
            RequiresApproval = requiresApproval,
            ApprovalThreshold = policy.AdjustmentApprovalThreshold,
            PolicyId = policy.PolicyId,
            RequestedBy = command.RequestedBy,
            RequestedAt = DateTime.UtcNow
        };

        _db.CreditAdjustmentRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        if (!requiresApproval)
        {
            // Below the bar: apply immediately, no checker involved.
            await _creditService.CreateAdjustmentBatchAsync(request, cancellationToken);
            request.Status = AdjustmentStatus.Applied;
            request.AppliedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return request;
        }

        await NotifyPendingAsync(request, cancellationToken);
        return request;
    }

    public async Task<CreditAdjustmentRequest> ApproveAsync(Guid requestId, Guid reviewerId, string? reviewNote, CancellationToken cancellationToken = default)
    {
        var request = await GetPendingForReviewAsync(requestId, reviewerId, cancellationToken);

        request.Status = AdjustmentStatus.Approved;
        request.ReviewedBy = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = reviewNote;

        await _creditService.CreateAdjustmentBatchAsync(request, cancellationToken);
        request.Status = AdjustmentStatus.Applied;
        request.AppliedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await NotifyReviewedAsync(request, approved: true, cancellationToken);
        return request;
    }

    public async Task<CreditAdjustmentRequest> RejectAsync(Guid requestId, Guid reviewerId, string reviewNote, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reviewNote))
            throw new InvalidOperationException("A review note is mandatory when rejecting.");

        var request = await GetPendingForReviewAsync(requestId, reviewerId, cancellationToken);

        request.Status = AdjustmentStatus.Rejected;
        request.ReviewedBy = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = reviewNote.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        await NotifyReviewedAsync(request, approved: false, cancellationToken);
        return request;
    }

    public Task<CreditAdjustmentRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
        => _db.CreditAdjustmentRequests
            .AsNoTracking()
            .Include(r => r.Brand)
            .Include(r => r.RelatedBatch)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

    public async Task<CreditAdjustmentResult> GetRequestsAsync(CreditAdjustmentFilters filters, CancellationToken cancellationToken = default)
    {
        var query = _db.CreditAdjustmentRequests
            .AsNoTracking()
            .Include(r => r.Brand)
            .AsQueryable();

        if (filters.BrandId.HasValue)
            query = query.Where(r => r.BrandId == filters.BrandId.Value);

        if (filters.Status.HasValue)
            query = query.Where(r => r.Status == filters.Status.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var requests = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancellationToken);

        return new CreditAdjustmentResult(requests, totalCount, filters.Page, filters.PageSize);
    }

    private async Task<CreditAdjustmentRequest> GetPendingForReviewAsync(Guid requestId, Guid reviewerId, CancellationToken cancellationToken)
    {
        var request = await _db.CreditAdjustmentRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException($"Adjustment request {requestId} not found.");

        if (request.Status != AdjustmentStatus.PendingApproval)
            throw new InvalidOperationException($"Request is {request.Status}; only PendingApproval requests can be reviewed.");

        if (request.RequestedBy == reviewerId)
            throw new InvalidOperationException("Self-approval is not allowed: the reviewer must differ from the requester.");

        return request;
    }

    private async Task NotifyPendingAsync(CreditAdjustmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var brandName = await _db.Brands
                .AsNoTracking()
                .Where(b => b.Id == request.BrandId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "(unknown brand)";

            var requesterName = await _db.UserAccounts
                .AsNoTracking()
                .Where(u => u.Id == request.RequestedBy)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken) ?? "(unknown user)";

            // Usernames double as emails when they contain '@' (no dedicated email field yet).
            var approverEmails = await _db.UserAccounts
                .AsNoTracking()
                .Where(u => u.Role == UserRole.FinancialController && u.Status == UserStatus.Active && u.Username.Contains("@"))
                .Select(u => u.Username)
                .ToListAsync(cancellationToken);

            await _notificationService.NotifyAdjustmentPendingAsync(new AdjustmentPendingNotification(
                request.Id, brandName, request.AdjustmentType.ToString(), request.Amount, requesterName, approverEmails),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Notifications must never fail the workflow.
            _logger.LogError(ex, "Failed to send pending-approval notification for adjustment {RequestId}.", request.Id);
        }
    }

    private async Task NotifyReviewedAsync(CreditAdjustmentRequest request, bool approved, CancellationToken cancellationToken)
    {
        try
        {
            var brandName = await _db.Brands
                .AsNoTracking()
                .Where(b => b.Id == request.BrandId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "(unknown brand)";

            var requesterEmail = await _db.UserAccounts
                .AsNoTracking()
                .Where(u => u.Id == request.RequestedBy && u.Username.Contains("@"))
                .Select(u => u.Username)
                .FirstOrDefaultAsync(cancellationToken);

            await _notificationService.NotifyAdjustmentReviewedAsync(new AdjustmentReviewedNotification(
                request.Id, brandName, request.AdjustmentType.ToString(), request.Amount, approved, request.ReviewNote, requesterEmail),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send review-result notification for adjustment {RequestId}.", request.Id);
        }
    }
}

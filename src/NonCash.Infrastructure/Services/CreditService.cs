using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// EF-backed prepaid credit service on the batch model (Epic 10).
/// Balance = SUM(RemainingAmount) over non-expired batches. Consumption drains FIFO
/// from the oldest non-expired batch, is idempotent per voucher (unique index on
/// voucher_detail_id) and never blocks the calling business operation (grace overdraft:
/// with no available batch, the newest batch goes negative).
/// </summary>
public class CreditService : ICreditService
{
    private readonly ApplicationDbContext _context;
    private readonly ICreditPolicyService _policyService;
    private readonly ILogger<CreditService> _logger;

    public CreditService(ApplicationDbContext context, ICreditPolicyService policyService, ILogger<CreditService> logger)
    {
        _context = context;
        _policyService = policyService;
        _logger = logger;
    }

    public async Task<int> GetBalanceAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.CreditBatches
            .AsNoTracking()
            .Where(b => b.BrandId == brandId && (b.ExpiresAt == null || b.ExpiresAt > now))
            .SumAsync(b => b.RemainingAmount, cancellationToken);
    }

    public async Task<bool> HasCreditAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await GetBalanceAsync(brandId, cancellationToken) > 0;
    }

    public async Task TryConsumeAsync(
        Guid brandId,
        Guid voucherDetailId,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Idempotency pre-check: skip if this voucher was already charged.
            var alreadyCharged = await _context.CreditConsumptions
                .AsNoTracking()
                .AnyAsync(c => c.VoucherDetailId == voucherDetailId, cancellationToken);

            if (alreadyCharged)
                return;

            var now = DateTime.UtcNow;

            // FIFO: oldest non-expired batch with remaining credits.
            var batch = await _context.CreditBatches
                .Where(b => b.BrandId == brandId
                    && b.RemainingAmount > 0
                    && (b.ExpiresAt == null || b.ExpiresAt > now))
                .OrderBy(b => b.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            // Grace overdraft: no available batch → newest batch goes negative.
            batch ??= await _context.CreditBatches
                .Where(b => b.BrandId == brandId)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (batch is null)
            {
                _logger.LogWarning(
                    "Credit consumption skipped for voucher {VoucherDetailId}: brand {BrandId} has no credit batches.",
                    voucherDetailId, brandId);
                return;
            }

            batch.RemainingAmount -= 1;

            _context.CreditConsumptions.Add(new CreditConsumption
            {
                BatchId = batch.Id,
                BrandId = brandId,
                VoucherDetailId = voucherDetailId,
                Reference = reference
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Unique index violation = concurrent charge for the same voucher → idempotent success.
            _logger.LogWarning(ex,
                "Credit consumption skipped for voucher {VoucherDetailId} (brand {BrandId}) — likely already charged.",
                voucherDetailId, brandId);
        }
        catch (Exception ex)
        {
            // Billing must never break the business operation (grace policy).
            _logger.LogError(ex,
                "Credit consumption failed for voucher {VoucherDetailId} (brand {BrandId}).",
                voucherDetailId, brandId);
        }
    }

    public async Task<CreditBatch> CreatePurchaseAsync(
        Guid brandId,
        int amount,
        string? reference,
        string? evidenceImageUrl,
        Guid? byUserId,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Purchase amount must be positive.", nameof(amount));

        var policy = await _policyService.ResolveForBrandAsync(brandId, cancellationToken);

        var batch = new CreditBatch
        {
            BrandId = brandId,
            PolicyId = policy.PolicyId,
            BatchType = CreditBatchType.Purchase,
            OriginalAmount = amount,
            RemainingAmount = amount,
            PricePerCreditVnd = policy.PricePerCreditVnd,
            TotalPaidVnd = policy.PricePerCreditVnd * amount,
            ExpiresAt = ToExpiry(policy.CreditExpiryMonths),
            EvidenceImageUrl = evidenceImageUrl,
            Reference = reference,
            CreatedBy = byUserId
        };

        _context.CreditBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<CreditBatch?> GrantWelcomeAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var policy = await _policyService.ResolveForBrandAsync(brandId, cancellationToken);
        if (policy.WelcomeCredits <= 0)
            return null;

        var alreadyGranted = await _context.CreditBatches
            .AsNoTracking()
            .AnyAsync(b => b.BrandId == brandId && b.BatchType == CreditBatchType.WelcomeGrant, cancellationToken);

        if (alreadyGranted)
            return null;

        var batch = new CreditBatch
        {
            BrandId = brandId,
            PolicyId = policy.PolicyId,
            BatchType = CreditBatchType.WelcomeGrant,
            OriginalAmount = policy.WelcomeCredits,
            RemainingAmount = policy.WelcomeCredits,
            PricePerCreditVnd = 0m,
            TotalPaidVnd = 0m,
            ExpiresAt = ToExpiry(policy.WelcomeCreditExpiryMonths),
            Reference = "Welcome credits on brand activation"
        };

        _context.CreditBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<CreditBatch> CreateAdjustmentBatchAsync(
        CreditAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AdjustmentType is CreditBatchType.Purchase or CreditBatchType.WelcomeGrant)
            throw new ArgumentException("Purchase/WelcomeGrant batches are not adjustment outcomes.", nameof(request));
        if (request.Amount <= 0)
            throw new ArgumentException("Adjustment amount must be positive.", nameof(request));

        var policy = await _policyService.ResolveForBrandAsync(request.BrandId, cancellationToken);

        // Clawback removes credits: represented as a negative, non-expiring batch.
        var signedAmount = request.AdjustmentType == CreditBatchType.Clawback ? -request.Amount : request.Amount;

        var batch = new CreditBatch
        {
            BrandId = request.BrandId,
            PolicyId = request.PolicyId ?? policy.PolicyId,
            BatchType = request.AdjustmentType,
            OriginalAmount = signedAmount,
            RemainingAmount = signedAmount,
            PricePerCreditVnd = 0m,
            TotalPaidVnd = 0m,
            ExpiresAt = signedAmount > 0 ? ToExpiry(policy.CreditExpiryMonths) : null,
            EvidenceImageUrl = request.EvidenceImageUrl,
            Reference = request.ReasonText,
            AdjustmentRequestId = request.Id,
            CreatedBy = request.RequestedBy
        };

        _context.CreditBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<CreditBatchResult> GetBatchesAsync(
        CreditBatchFilters filters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CreditBatches
            .Include(b => b.Brand)
            .AsNoTracking()
            .AsQueryable();

        if (filters.BrandId.HasValue)
            query = query.Where(b => b.BrandId == filters.BrandId.Value);

        if (filters.BatchType.HasValue)
            query = query.Where(b => b.BatchType == filters.BatchType.Value);

        if (filters.FromDate.HasValue)
            query = query.Where(b => b.CreatedAt >= filters.FromDate.Value);

        if (filters.ToDate.HasValue)
            query = query.Where(b => b.CreatedAt <= filters.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var batches = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancellationToken);

        return new CreditBatchResult(batches, totalCount, filters.Page, filters.PageSize);
    }

    public async Task<CreditConsumptionResult> GetConsumptionsAsync(
        Guid brandId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CreditConsumptions
            .AsNoTracking()
            .Where(c => c.BrandId == brandId);

        var totalCount = await query.CountAsync(cancellationToken);

        var consumptions = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new CreditConsumptionResult(consumptions, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<CreditBatch>> GetExpiringBatchesAsync(
        Guid brandId,
        int withinDays,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(withinDays);

        return await _context.CreditBatches
            .AsNoTracking()
            .Where(b => b.BrandId == brandId
                && b.RemainingAmount > 0
                && b.ExpiresAt != null
                && b.ExpiresAt > now
                && b.ExpiresAt <= cutoff)
            .OrderBy(b => b.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    private static DateTime? ToExpiry(int? months)
        => months is > 0 ? DateTime.UtcNow.AddMonths(months.Value) : null;
}

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
    private readonly IWelcomePolicyService _welcomePolicyService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CreditService> _logger;

    public CreditService(
        ApplicationDbContext context,
        ICreditPolicyService policyService,
        IWelcomePolicyService welcomePolicyService,
        INotificationService notificationService,
        ILogger<CreditService> logger)
    {
        _context = context;
        _policyService = policyService;
        _welcomePolicyService = welcomePolicyService;
        _notificationService = notificationService;
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

            var balanceBefore = await GetBalanceAsync(brandId, cancellationToken);

            batch.RemainingAmount -= 1;

            _context.CreditConsumptions.Add(new CreditConsumption
            {
                BatchId = batch.Id,
                BrandId = brandId,
                VoucherDetailId = voucherDetailId,
                Reference = reference
            });

            await _context.SaveChangesAsync(cancellationToken);
            await NotifyLowBalanceAsync(brandId, balanceBefore, cancellationToken);
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

    public async Task<CreditFundingResult> EvaluatePlanFundingAsync(Guid brandId, int requiredQuantity, CancellationToken cancellationToken = default)
    {
        var balance = await GetBalanceAsync(brandId, cancellationToken);
        return new CreditFundingResult(balance >= requiredQuantity, balance, requiredQuantity);
    }

    public async Task<bool> TryConsumeForPlanAsync(Guid brandId, Guid planId, int quantity, string? reference = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) return true;

        try
        {
            // Idempotent per plan: a plan is charged once at approval.
            var alreadyCharged = await _context.CreditConsumptions.AnyAsync(c => c.PlanId == planId, cancellationToken);
            if (alreadyCharged) return true;

            // Hard check: refuse to charge when the brand cannot fund the full approved quantity.
            var funding = await EvaluatePlanFundingAsync(brandId, quantity, cancellationToken);
            if (!funding.Sufficient)
            {
                _logger.LogInformation(
                    "Plan approval charge refused for BrandId={BrandId} PlanId={PlanId}: balance {Balance} < required {Required}",
                    brandId, planId, funding.Balance, funding.Required);
                return false;
            }

            // Drain FIFO (soonest-expiring first) across non-expired batches; record one aggregate plan row.
            var remaining = quantity;
            var now = DateTime.UtcNow;
            var batches = await _context.CreditBatches
                .Where(b => b.BrandId == brandId && b.RemainingAmount > 0 && (b.ExpiresAt == null || b.ExpiresAt > now))
                .OrderBy(b => b.ExpiresAt ?? DateTime.MaxValue)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var batch in batches)
            {
                if (remaining == 0) break;
                var take = Math.Min(remaining, batch.RemainingAmount);
                batch.RemainingAmount -= take;
                remaining -= take;
            }

            _context.CreditConsumptions.Add(new CreditConsumption
            {
                BatchId = null,
                BrandId = brandId,
                PlanId = planId,
                Quantity = quantity,
                Reference = reference ?? "plan-approval"
            });

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            // Concurrent double-approve hit the unique plan index → treat as already charged (idempotent).
            _logger.LogInformation(ex, "Plan approval charge already recorded for PlanId={PlanId}", planId);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-closed: if the charge errors, approval must not proceed.
            _logger.LogError(ex, "TryConsumeForPlan failed for BrandId={BrandId} PlanId={PlanId}", brandId, planId);
            return false;
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

        await NotifyCreditPurchasedAsync(brandId, batch, cancellationToken);
        return batch;
    }

    public async Task<CreditBatch?> GrantWelcomeAsync(Guid brandId, bool sendNotification = true, CancellationToken cancellationToken = default)
    {
        // Welcome is a per-business commercial term: resolve the brand's business, then
        // the business's welcome policy (→ CreditConfig fallback).
        var businessId = await _context.Brands
            .AsNoTracking()
            .Where(b => b.Id == brandId)
            .Select(b => b.BusinessId)
            .FirstOrDefaultAsync(cancellationToken);

        if (businessId == Guid.Empty)
            return null;

        var welcome = await _welcomePolicyService.ResolveForBusinessAsync(businessId, cancellationToken);
        if (welcome.WelcomeCredits <= 0)
            return null;

        // Idempotent per brand: one welcome batch per brand, ever.
        var alreadyGranted = await _context.CreditBatches
            .AsNoTracking()
            .AnyAsync(b => b.BrandId == brandId && b.BatchType == CreditBatchType.WelcomeGrant, cancellationToken);

        if (alreadyGranted)
            return null;

        var batch = new CreditBatch
        {
            BrandId = brandId,
            WelcomePolicyId = welcome.PolicyId,
            BatchType = CreditBatchType.WelcomeGrant,
            OriginalAmount = welcome.WelcomeCredits,
            RemainingAmount = welcome.WelcomeCredits,
            PricePerCreditVnd = 0m,
            TotalPaidVnd = 0m,
            ExpiresAt = ToExpiry(welcome.WelcomeCreditExpiryMonths),
            Reference = "Welcome credits on brand activation"
        };

        _context.CreditBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);

        if (sendNotification)
        {
            await NotifyWelcomeCreditGrantedAsync(brandId, batch, cancellationToken);
        }
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
        // All-time consumed credits (SUM of row quantities), so the UI never mistakes
        // the row count for the charged amount (one plan-approval row carries Quantity=N).
        var totalQuantity = await query.SumAsync(c => (int?)c.Quantity, cancellationToken) ?? 0;

        var pageRows = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Enrich the page with human-readable names (one lookup per referenced id set).
        var planIds = pageRows.Where(c => c.PlanId.HasValue).Select(c => c.PlanId!.Value).Distinct().ToList();
        var detailIds = pageRows.Where(c => c.VoucherDetailId.HasValue).Select(c => c.VoucherDetailId!.Value).Distinct().ToList();

        var planNames = planIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _context.VoucherPlanHeaders
                .AsNoTracking()
                .Where(p => planIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        var detailById = detailIds.Count == 0
            ? new Dictionary<Guid, VoucherPlanDetail>()
            : await _context.VoucherPlanDetails
                .AsNoTracking()
                .Where(d => detailIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, cancellationToken);

        // Per-voucher rows reach their plan name through the detail's ParentId.
        var parentIds = detailById.Values.Select(d => d.ParentId).Distinct().Where(id => !planNames.ContainsKey(id)).ToList();
        if (parentIds.Count > 0)
        {
            var parentNames = await _context.VoucherPlanHeaders
                .AsNoTracking()
                .Where(p => parentIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);
            foreach (var kv in parentNames)
                planNames[kv.Key] = kv.Value;
        }

        var items = pageRows.Select(c =>
        {
            string? planName = null;
            string? serialNo = null;
            if (c.PlanId.HasValue)
                planNames.TryGetValue(c.PlanId.Value, out planName);
            if (c.VoucherDetailId.HasValue && detailById.TryGetValue(c.VoucherDetailId.Value, out var detail))
            {
                serialNo = detail.SerialNo;
                planNames.TryGetValue(detail.ParentId, out planName);
            }
            return new CreditConsumptionItem(c, planName, serialNo);
        }).ToList();

        return new CreditConsumptionResult(items, totalCount, totalQuantity, page, pageSize);
    }

    public async Task<CreditLedgerTotals> GetLedgerTotalsAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        // Granted = credits that entered the wallet (negative clawback batches excluded).
        var granted = await _context.CreditBatches
            .AsNoTracking()
            .Where(b => b.BrandId == brandId && b.OriginalAmount > 0)
            .SumAsync(b => (int?)b.OriginalAmount, cancellationToken) ?? 0;

        var consumed = await _context.CreditConsumptions
            .AsNoTracking()
            .Where(c => c.BrandId == brandId)
            .SumAsync(c => (int?)c.Quantity, cancellationToken) ?? 0;

        return new CreditLedgerTotals(granted, consumed);
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

    private async Task NotifyWelcomeCreditGrantedAsync(Guid brandId, CreditBatch batch, CancellationToken cancellationToken)
    {
        try
        {
            var brand = await _context.Brands
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
            if (brand is null) return;

            await _notificationService.NotifyWelcomeCreditGrantedAsync(new WelcomeCreditGrantedNotification(
                brand.ContactEmail, brand.Name, batch.OriginalAmount, batch.ExpiresAt), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome-credit notification for brand {BrandId}.", brandId);
        }
    }

    private async Task NotifyCreditPurchasedAsync(Guid brandId, CreditBatch batch, CancellationToken cancellationToken)
    {
        try
        {
            var brand = await _context.Brands
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
            if (brand is null) return;

            await _notificationService.NotifyCreditPurchasedAsync(new CreditPurchasedNotification(
                brand.ContactEmail, brand.Name, batch.OriginalAmount, batch.TotalPaidVnd, batch.ExpiresAt, batch.Reference), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send credit-purchase notification for brand {BrandId}.", brandId);
        }
    }

    private async Task NotifyLowBalanceAsync(Guid brandId, int balanceBefore, CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _policyService.ResolveForBrandAsync(brandId, cancellationToken);
            if (policy.LowBalanceWarningPct is null or <= 0)
                return;

            var totalGranted = await _context.CreditBatches
                .AsNoTracking()
                .Where(b => b.BrandId == brandId && b.OriginalAmount > 0)
                .SumAsync(b => (int?)b.OriginalAmount, cancellationToken) ?? 0;

            if (totalGranted <= 0)
                return;

            var threshold = (int)Math.Ceiling(totalGranted * policy.LowBalanceWarningPct.Value / 100m);
            var balanceAfter = balanceBefore - 1;

            if (balanceBefore > threshold && balanceAfter <= threshold)
            {
                var brand = await _context.Brands
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
                if (brand is null) return;

                await _notificationService.NotifyLowCreditBalanceAsync(new LowCreditBalanceNotification(
                    brand.ContactEmail, brand.Name, balanceAfter, threshold, totalGranted), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send low-balance notification for brand {BrandId}.", brandId);
        }
    }

    private static DateTime? ToExpiry(int? months)
        => months is > 0 ? DateTime.UtcNow.AddMonths(months.Value) : null;
}

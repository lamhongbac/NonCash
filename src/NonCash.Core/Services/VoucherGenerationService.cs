using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public interface IVoucherGenerationService
{
    Task<GenerationResult> GenerateBatchAsync(Guid planId, int quantity, Guid brandId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherPlanDetail>> ListByPlanAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default);
}

public record GenerationResult(bool Success, int GeneratedCount = 0, string? ErrorMessage = null);

public class VoucherGenerationService : IVoucherGenerationService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IVoucherCodeService _voucherCodeService;
    private readonly IRepository<VoucherPlanDetail> _detailRepository;

    public VoucherGenerationService(
        IVoucherPlanRepository planRepository,
        IVoucherCodeService voucherCodeService,
        IRepository<VoucherPlanDetail> detailRepository)
    {
        _planRepository = planRepository;
        _voucherCodeService = voucherCodeService;
        _detailRepository = detailRepository;
    }

    public async Task<GenerationResult> GenerateBatchAsync(Guid planId, int quantity, Guid brandId, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            return new GenerationResult(false, ErrorMessage: "Quantity must be greater than 0.");

        if (quantity > 10000)
            return new GenerationResult(false, ErrorMessage: "Maximum batch size is 10,000 vouchers per request.");

        var plan = await _planRepository.GetByIdWithOutletsAsync(planId, cancellationToken);
        if (plan == null)
            return new GenerationResult(false, ErrorMessage: "Plan not found.");

        if (plan.BrandId != brandId)
            return new GenerationResult(false, ErrorMessage: "You do not have access to this plan.");

        // AC1: Approval Gate
        if (plan.ApprovalStatus != ApprovalStatus.Approved)
            return new GenerationResult(false, ErrorMessage: "PlanNotApproved: Vouchers can only be generated for approved plans.");

        // Generate voucher details
        // Serial number prefix uses PlanId (8-char hex) to guarantee global uniqueness across
        // all plans of the same brand. Brand code alone would collide when multiple plans exist.
        var planPrefix = planId.ToString()[..8].ToUpperInvariant();
        var year = DateTime.UtcNow.Year;
        var details = new List<VoucherPlanDetail>();

        // Get existing count for sequential numbering
        var existing = await _detailRepository.FindAsync(d => d.ParentId == planId, cancellationToken);
        var startSeq = existing.Count() + 1;

        // Approved-quantity gate: total generated vouchers must never exceed the plan's TargetQuantity
        var remaining = plan.TargetQuantity - existing.Count();
        if (quantity > remaining)
            return new GenerationResult(false, ErrorMessage: $"QuantityExceedsRemaining: Only {remaining} vouchers remain for this plan (approved {plan.TargetQuantity}, generated {existing.Count()}).");

        for (int i = 0; i < quantity; i++)
        {
            var seq = startSeq + i;
            var serialNo = $"VC-{planPrefix}-{year}-{seq:D8}";
            var secretKey = _voucherCodeService.GenerateSecretKey();

            var detail = new VoucherPlanDetail
            {
                ParentId = planId,
                SerialNo = serialNo,
                VoucherCodeSecret = secretKey,
                UsageStatus = UsageStatus.Pending
            };

            details.Add(detail);
            await _detailRepository.AddAsync(detail, cancellationToken);
        }

        await _detailRepository.SaveChangesAsync(cancellationToken);

        // Generation only materializes the paid quota as voucher rows (the distributable pool).
        // It is NOT a distribution, so the plan header (TargetDistributed) is intentionally left
        // untouched here — that counter is reconciled when vouchers are actually handed out.
        return new GenerationResult(true, GeneratedCount: quantity);
    }

    public async Task<IReadOnlyList<VoucherPlanDetail>> ListByPlanAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || plan.BrandId != brandId)
            return Array.Empty<VoucherPlanDetail>();

        var details = await _detailRepository.FindAsync(d => d.ParentId == planId, cancellationToken);
        return details.OrderBy(d => d.SerialNo).ToList();
    }
}

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
        var brandCode = plan.Brand?.TaxCode ?? plan.BrandId.ToString()[..8].ToUpperInvariant();
        var year = DateTime.UtcNow.Year;
        var details = new List<VoucherPlanDetail>();

        // Get existing count for sequential numbering
        var existing = await _detailRepository.FindAsync(d => d.ParentId == planId, cancellationToken);
        var startSeq = existing.Count() + 1;

        for (int i = 0; i < quantity; i++)
        {
            var seq = startSeq + i;
            var serialNo = $"VC-{brandCode}-{year}-{seq:D8}";
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

        // Update the plan's distributed count
        plan.TargetDistributed += quantity;
        _planRepository.Update(plan);
        await _planRepository.SaveChangesAsync(cancellationToken);

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

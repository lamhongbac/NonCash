using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class PromotionService : IPromotionService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;

    public PromotionService(
        IVoucherPlanRepository planRepository,
        IRepository<VoucherPlanDetail> detailRepository,
        ICustomerRepository customerRepository,
        IRepository<VoucherDistribution> distributionRepository)
    {
        _planRepository = planRepository;
        _detailRepository = detailRepository;
        _customerRepository = customerRepository;
        _distributionRepository = distributionRepository;
    }

    public async Task<PromotionResult> DistributeAsync(
        Guid planId,
        Guid brandId,
        IReadOnlyList<string> phoneNumbers,
        CancellationToken cancellationToken = default)
    {
        if (phoneNumbers == null || phoneNumbers.Count == 0)
            return new PromotionResult(false, ErrorCode: "EmptyList", ErrorMessage: "Phone number list is empty.");

        // AC1: Plan must exist and belong to brand
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null)
            return new PromotionResult(false, ErrorCode: "NotFound", ErrorMessage: "Plan not found.");

        if (plan.BrandId != brandId)
            return new PromotionResult(false, ErrorCode: "Forbidden", ErrorMessage: "You do not have access to this plan.");

        // AC1: Plan must be Approved (Published is a future state; treat Approved as eligible)
        if (plan.ApprovalStatus != ApprovalStatus.Approved)
            return new PromotionResult(false, ErrorCode: "PlanNotApproved", ErrorMessage: "Only approved plans can be promoted.");

        // Normalize and dedupe phone numbers preserving order
        var normalized = new List<string>();
        var invalidPhones = new List<SkippedRecord>();
        var seen = new HashSet<string>();
        foreach (var raw in phoneNumbers)
        {
            var n = Customer.NormalizePhoneNumber(raw ?? string.Empty);
            if (string.IsNullOrEmpty(n))
            {
                invalidPhones.Add(new SkippedRecord(raw ?? string.Empty, "InvalidPhoneNumber"));
                continue;
            }
            if (seen.Add(n))
                normalized.Add(n);
        }

        if (normalized.Count == 0)
            return new PromotionResult(false, ErrorCode: "NoValidPhones", ErrorMessage: "No valid phone numbers in list.", SkippedRecords: invalidPhones);

        // AC2 + AC5: Resolve customers (create missing, skip blacklisted)
        var skipped = new List<SkippedRecord>(invalidPhones);
        var eligibleCustomerIds = new List<(string Phone, Guid CustomerId)>();
        foreach (var phone in normalized)
        {
            var existing = await _customerRepository.GetByPhoneNumberAsync(phone, cancellationToken);
            if (existing == null)
            {
                var newCustomer = new Customer
                {
                    PhoneNumber = phone,
                    FullName = phone, // AC2: minimal record; full name unknown for promotion-only entries
                    Status = CustomerStatus.Active
                };
                await _customerRepository.AddAsync(newCustomer, cancellationToken);
                eligibleCustomerIds.Add((phone, newCustomer.Id));
            }
            else if (existing.Status == CustomerStatus.Blacklisted)
            {
                skipped.Add(new SkippedRecord(phone, "Blacklisted"));
            }
            else
            {
                eligibleCustomerIds.Add((phone, existing.Id));
            }
        }

        if (eligibleCustomerIds.Count == 0)
        {
            return new PromotionResult(false, ErrorCode: "NoEligibleCustomers", ErrorMessage: "All provided customers are blacklisted or invalid.", SkippedRecords: skipped);
        }

        // AC1 + AC4: Stock check (Pending and unassigned)
        var available = (await _detailRepository.FindAsync(
            d => d.ParentId == planId && d.MemberId == null && d.UsageStatus == UsageStatus.Pending,
            cancellationToken)).OrderBy(d => d.SerialNo).ToList();

        if (available.Count < eligibleCustomerIds.Count)
        {
            return new PromotionResult(
                false,
                ErrorCode: "InsufficientStock",
                ErrorMessage: $"Insufficient voucher stock. Required: {eligibleCustomerIds.Count}, Available: {available.Count}.",
                SkippedRecords: skipped);
        }

        // AC3: Allocate one voucher per customer; AC4: all-or-nothing handled by single SaveChangesAsync
        var now = DateTime.UtcNow;
        for (var i = 0; i < eligibleCustomerIds.Count; i++)
        {
            var (_, customerId) = eligibleCustomerIds[i];

            // Re-attach a tracked entity (FindAsync returned AsNoTracking entries)
            var trackedDetail = await _detailRepository.GetByIdAsync(available[i].Id, cancellationToken);
            if (trackedDetail == null || trackedDetail.MemberId != null)
            {
                return new PromotionResult(
                    false,
                    ErrorCode: "ConcurrencyConflict",
                    ErrorMessage: "Voucher stock changed during allocation. Please retry.",
                    SkippedRecords: skipped);
            }

            trackedDetail.MemberId = customerId;
            _detailRepository.Update(trackedDetail);

            var distribution = new VoucherDistribution
            {
                VoucherId = trackedDetail.Id,
                MemberId = customerId,
                Method = DistributionMethod.Promotion,
                DistributionDate = now
            };
            await _distributionRepository.AddAsync(distribution, cancellationToken);
        }

        // AC6: Update plan distribution counter
        plan.TargetDistributed += eligibleCustomerIds.Count;
        _planRepository.Update(plan);

        // Single atomic save (EF Core wraps in implicit transaction)
        await _planRepository.SaveChangesAsync(cancellationToken);

        return new PromotionResult(
            Success: true,
            DistributedCount: eligibleCustomerIds.Count,
            SkippedCount: skipped.Count,
            SkippedRecords: skipped);
    }
}

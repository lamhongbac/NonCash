using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class PromotionService : IPromotionService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;
    private readonly IRepository<VoucherUsage> _usageRepository;
    private readonly IVoucherTransferRepository _transferRepository;
    private readonly IRepository<Outlet> _outletRepository;
    private readonly INotificationService _notificationService;
    private readonly IBrandCustomerRepository _brandCustomerRepository;
    private readonly IRepository<VoucherDistributionBatch> _batchRepository;

    private static readonly System.Text.RegularExpressions.Regex _emailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public PromotionService(
        IVoucherPlanRepository planRepository,
        IRepository<VoucherPlanDetail> detailRepository,
        ICustomerRepository customerRepository,
        IMemberAccountRepository memberRepository,
        IRepository<VoucherDistribution> distributionRepository,
        IRepository<VoucherUsage> usageRepository,
        IVoucherTransferRepository transferRepository,
        IRepository<Outlet> outletRepository,
        INotificationService notificationService,
        IBrandCustomerRepository brandCustomerRepository,
        IRepository<VoucherDistributionBatch> batchRepository)
    {
        _planRepository = planRepository;
        _detailRepository = detailRepository;
        _customerRepository = customerRepository;
        _memberRepository = memberRepository;
        _distributionRepository = distributionRepository;
        _usageRepository = usageRepository;
        _transferRepository = transferRepository;
        _outletRepository = outletRepository;
        _notificationService = notificationService;
        _brandCustomerRepository = brandCustomerRepository;
        _batchRepository = batchRepository;
    }

    public async Task<PromotionResult> DistributeAsync(
        Guid planId,
        Guid brandId,
        IReadOnlyList<string> phoneNumbers,
        NotificationChannel notifyChannels = NotificationChannel.Email,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? phoneToEmail = null,
        Guid? createdById = null)
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

        // Recipient rules (strict): every token must resolve to an existing customer —
        // recipients are NEVER auto-created from this list. Email tokens resolve to the
        // matching customer's phone so a mixed phone/email list works. Duplicates are
        // reported and skipped: a customer receives exactly one voucher no matter how
        // many times they appear in the list.
        var incoming = new List<(string Token, string PhoneOrRaw)>();
        var emailSkips = new List<SkippedRecord>();
        var duplicateSkips = new List<SkippedRecord>();
        var seenTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in phoneNumbers)
        {
            var token = (raw ?? string.Empty).Trim();
            if (token.Length == 0)
                continue;
            if (!seenTokens.Add(token))
            {
                duplicateSkips.Add(new SkippedRecord(token, "Duplicate"));
                continue;
            }
            if (IsEmailToken(token))
            {
                var byEmail = await _customerRepository.GetByEmailAsync(token, cancellationToken);
                if (byEmail == null)
                {
                    emailSkips.Add(new SkippedRecord(token, "NoCustomerForEmail"));
                }
                else if (byEmail.Status == CustomerStatus.Blacklisted)
                {
                    emailSkips.Add(new SkippedRecord(token, "Blacklisted"));
                }
                else if (await _brandCustomerRepository.IsBlockedAsync(brandId, byEmail.Id, cancellationToken))
                {
                    emailSkips.Add(new SkippedRecord(token, "BrandBlocked"));
                }
                else if (string.IsNullOrWhiteSpace(byEmail.PhoneNumber))
                {
                    emailSkips.Add(new SkippedRecord(token, "NoPhoneForEmail"));
                }
                else
                {
                    incoming.Add((token, byEmail.PhoneNumber));
                }
                continue;
            }

            incoming.Add((token, token));
        }

        // Normalize and dedupe by customer (normalized phone) preserving order. The same
        // customer appearing under different spellings — or once as email and once as
        // phone — is only sent to once; the later occurrences are reported as duplicates.
        var normalized = new List<string>();
        var invalidPhones = new List<SkippedRecord>();
        var seenPhones = new HashSet<string>();
        foreach (var (token, phoneOrRaw) in incoming)
        {
            var n = Customer.NormalizePhoneNumber(phoneOrRaw);
            if (string.IsNullOrEmpty(n))
            {
                invalidPhones.Add(new SkippedRecord(token, "InvalidPhoneNumber"));
                continue;
            }
            if (!seenPhones.Add(n))
            {
                duplicateSkips.Add(new SkippedRecord(token, "Duplicate"));
                continue;
            }
            normalized.Add(n);
        }

        var preSkips = invalidPhones.Concat(emailSkips).Concat(duplicateSkips).ToList();
        if (normalized.Count == 0)
            return new PromotionResult(false, ErrorCode: "NoValidRecipients", ErrorMessage: "No valid recipients in list.", SkippedRecords: preSkips);

        // AC2 + AC5: Classify recipients WITHOUT mutating anything yet. The stock pre-check must
        // run before anything is written, so an insufficient-stock request leaves the database
        // completely untouched (no silent side effects). Unknown phones are skipped here —
        // a recipient only qualifies when the customer record already exists.
        var skipped = new List<SkippedRecord>(preSkips);
        var eligible = new List<(string Phone, Customer Customer)>();
        foreach (var phone in normalized)
        {
            var existing = await _customerRepository.GetByPhoneNumberAsync(phone, cancellationToken);
            if (existing == null)
            {
                skipped.Add(new SkippedRecord(phone, "CustomerNotFound"));
            }
            else if (existing.Status == CustomerStatus.Blacklisted)
            {
                skipped.Add(new SkippedRecord(phone, "Blacklisted"));
            }
            else if (await _brandCustomerRepository.IsBlockedAsync(brandId, existing.Id, cancellationToken))
            {
                skipped.Add(new SkippedRecord(phone, "BrandBlocked"));
            }
            else
            {
                eligible.Add((phone, existing));
            }
        }

        if (eligible.Count == 0)
        {
            return new PromotionResult(false, ErrorCode: "NoEligibleCustomers", ErrorMessage: "No valid existing customers in the recipient list.", SkippedRecords: skipped);
        }

        // AC1 + AC4: Stock pre-check (Pending and unassigned) — runs BEFORE any mutation.
        var allDetails = (await _detailRepository.FindAsync(d => d.ParentId == planId, cancellationToken)).ToList();
        var available = allDetails
            .Where(d => d.MemberId == null && d.UsageStatus == UsageStatus.Pending)
            .OrderBy(d => d.SerialNo)
            .ToList();
        var assignedBefore = allDetails.Count(d => d.MemberId != null);
        var assignedUsed = allDetails.Count - available.Count;

        if (available.Count < eligible.Count)
        {
            return new PromotionResult(
                false,
                ErrorCode: "InsufficientStock",
                ErrorMessage: $"Nothing was distributed. Required {eligible.Count}, Available {available.Count} ({assignedUsed} already assigned/used). Generate more vouchers for this plan or reduce the recipient list.",
                SkippedRecords: skipped);
        }

        // Stock is sufficient — now resolve member accounts for the verified existing recipients.
        var eligibleMembers = new List<(string Phone, Guid MemberId, string? Email, string Name)>();
        foreach (var (phone, customer) in eligible)
        {
            // Auto-link: receiving this brand's promotion makes them this brand's customer.
            await _brandCustomerRepository.EnsureAsync(brandId, customer.Id, BrandCustomerSource.PromotionAuto, null, cancellationToken);

            // Upsert email from integration payload if not already on file
            var suppliedEmail = Customer.NormalizeEmail(ResolveEmail(phone, phoneToEmail));
            if (!string.IsNullOrEmpty(suppliedEmail) && string.IsNullOrEmpty(customer.Email))
            {
                // Email is unique per customer: only adopt it if no other customer owns it
                if (!await _customerRepository.EmailExistsAsync(suppliedEmail, excludeCustomerId: customer.Id, cancellationToken: cancellationToken))
                {
                    customer.Email = suppliedEmail;
                    _customerRepository.Update(customer);
                    await _customerRepository.SaveChangesAsync(cancellationToken);
                }
            }

            var member = await EnsureMemberAccountAsync(customer, cancellationToken);
            eligibleMembers.Add((phone, member.Id, customer.Email, customer.FullName));
        }

        // AC3: Allocate one voucher per member; AC4: all-or-nothing handled by single SaveChangesAsync
        var now = DateTime.UtcNow;

        // Every batch promotion run is persisted as a traceable batch record; each distribution
        // row links back to it. Id is assigned up-front so the detail rows can reference it
        // within the same atomic SaveChanges (BaseEntity ids are otherwise set at save time).
        var batch = new VoucherDistributionBatch
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            BrandId = brandId,
            CreatedById = createdById,
            NotifyChannel = notifyChannels,
            RecipientCount = eligibleMembers.Count,
            DistributedCount = eligibleMembers.Count,
            SkippedCount = skipped.Count,
            SkippedRecords = skipped
                .Select(s => new BatchSkippedRecipient { PhoneNumber = s.PhoneNumber, Reason = s.Reason })
                .ToList()
        };
        await _batchRepository.AddAsync(batch, cancellationToken);

        for (var i = 0; i < eligibleMembers.Count; i++)
        {
            var (_, memberId, _, _) = eligibleMembers[i];

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

            trackedDetail.MemberId = memberId;
            _detailRepository.Update(trackedDetail);

            var distribution = new VoucherDistribution
            {
                VoucherId = trackedDetail.Id,
                MemberId = memberId,
                Method = DistributionMethod.Promotion,
                DistributionDate = now,
                BatchId = batch.Id
            };
            await _distributionRepository.AddAsync(distribution, cancellationToken);
        }

        // AC6: Reconcile the plan's distributed counter to the real number of vouchers handed out
        // (assigned detail rows) instead of blindly incrementing. This keeps target_distributed
        // equal to the actual distribution count and self-heals any historical inflation.
        plan.TargetDistributed = assignedBefore + eligibleMembers.Count;
        _planRepository.Update(plan);

        // Single atomic save (EF Core wraps in implicit transaction)
        await _planRepository.SaveChangesAsync(cancellationToken);

        // Notify recipients on the requested channels; delivery failures never fail the distribution.
        if (notifyChannels != NotificationChannel.None)
        {
            foreach (var (phone, _, email, name) in eligibleMembers)
            {
                try
                {
                    await _notificationService.NotifyVoucherReceivedAsync(
                        new VoucherReceivedNotification(
                            email,
                            phone,
                            name,
                            plan.DisplayName,
                            plan.FaceValue,
                            plan.ExpiryDate,
                            notifyChannels),
                        cancellationToken);
                }
                catch
                {
                    // Best-effort: notification errors are logged inside the service.
                }
            }
        }

        return new PromotionResult(
            Success: true,
            DistributedCount: eligibleMembers.Count,
            SkippedCount: skipped.Count,
            SkippedRecords: skipped,
            BatchId: batch.Id);
    }

    private async Task<MemberAccount> EnsureMemberAccountAsync(Customer customer, CancellationToken cancellationToken)
    {
        var existing = await _memberRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
        if (existing != null)
            return existing;

        var placeholder = new MemberAccount
        {
            CustomerId = customer.Id,
            Username = customer.PhoneNumber,
            PasswordHash = string.Empty,
            FullName = customer.FullName,
            Status = MemberAccountStatus.Active
        };
        return await _memberRepository.AddAsync(placeholder, cancellationToken);
    }

    // Epic 6.3: Wallet query for Integration API
    public async Task<IReadOnlyList<MemberWalletVoucher>> GetMemberVouchersByPhoneAsync(
        string phone, List<Guid> brandIds, CancellationToken cancellationToken = default)
    {
        var normalized = Customer.NormalizePhoneNumber(phone);
        var customer = await _customerRepository.GetByPhoneNumberAsync(normalized, cancellationToken);
        if (customer == null) return new List<MemberWalletVoucher>();

        var member = await _memberRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
        if (member == null) return new List<MemberWalletVoucher>();

        // Load all vouchers for this member, joined with plan header for display fields
        var details = await _detailRepository.FindAsync(
            d => d.MemberId == member.Id, cancellationToken);

        var result = new List<MemberWalletVoucher>();
        foreach (var v in details)
        {
            var plan = await _planRepository.GetByIdAsync(v.ParentId, cancellationToken);
            if (plan == null || !brandIds.Contains(plan.BrandId)) continue;

            result.Add(new MemberWalletVoucher(
                v.Id,
                v.SerialNo,
                plan.FaceValue,
                plan.ValueType,
                plan.ExpiryDate,
                v.UsageStatus,
                plan.ImageUrl,
                plan.IconUrl,
                plan.CoverImageUrl,
                plan.BrandColor,
                plan.DisplayName ?? plan.DisplayName,
                plan.ShortDescription,
                plan.TermsAndConditions,
                plan.Brand?.Name));
        }
        return result;
    }

    // Epic 6.3: Event history for Integration API
    public async Task<IReadOnlyList<MemberEventRecord>> GetMemberEventsByPhoneAsync(
        string phone, List<Guid> brandIds, int limit, CancellationToken cancellationToken = default)
    {
        var normalized = Customer.NormalizePhoneNumber(phone);
        var customer = await _customerRepository.GetByPhoneNumberAsync(normalized, cancellationToken);
        if (customer == null) return new List<MemberEventRecord>();

        var member = await _memberRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
        if (member == null) return new List<MemberEventRecord>();

        var events = new List<MemberEventRecord>();

        // 1. Distribution events
        var distributions = await _distributionRepository.FindAsync(
            d => d.MemberId == member.Id, cancellationToken);
        foreach (var dist in distributions)
        {
            // VoucherDistribution.VoucherId points to VoucherPlanDetail.Id; get the plan via ParentId
            var detail = await _detailRepository.GetByIdAsync(dist.VoucherId, cancellationToken);
            if (detail == null) continue;
            var header = await _planRepository.GetByIdAsync(detail.ParentId, cancellationToken);
            if (header == null || !brandIds.Contains(header.BrandId)) continue;

            events.Add(new MemberEventRecord(
                "Distributed",
                dist.DistributionDate,
                dist.VoucherId,
                detail.SerialNo,
                header.Brand?.Name ?? header.DisplayName,
                $"Method: {dist.Method}"));
        }

        // 2. Redemption events (from VoucherUsage)
        var memberVoucherIds = distributions.Select(d => d.VoucherId).ToList();
        var usages = await _usageRepository.FindAsync(
            u => memberVoucherIds.Contains(u.VoucherId), cancellationToken);
        foreach (var usage in usages)
        {
            var detail = await _detailRepository.GetByIdAsync(usage.VoucherId, cancellationToken);
            if (detail == null) continue;
            var header = await _planRepository.GetByIdAsync(detail.ParentId, cancellationToken);
            if (header == null || !brandIds.Contains(header.BrandId)) continue;

            events.Add(new MemberEventRecord(
                "Redeemed",
                usage.UsageDate,
                usage.VoucherId,
                detail.SerialNo,
                header.Brand?.Name ?? header.DisplayName,
                $"TransactionId: {usage.TransactionId}, Amount: {usage.AmountUsed}"));
        }

        // 3. Transfer events (sent or received)
        var transfers = await _transferRepository.FindAsync(
            t => t.SenderId == member.Id || t.RecipientId == member.Id, cancellationToken);
        foreach (var transfer in transfers)
        {
            var detail = await _detailRepository.GetByIdAsync(transfer.VoucherId, cancellationToken);
            if (detail == null) continue;
            var header = await _planRepository.GetByIdAsync(detail.ParentId, cancellationToken);
            if (header == null || !brandIds.Contains(header.BrandId)) continue;

            var direction = transfer.SenderId == member.Id ? "Sent" : "Received";
            events.Add(new MemberEventRecord(
                $"Transfer{direction}",
                transfer.InitiatedAt,
                transfer.VoucherId,
                detail.SerialNo,
                header.Brand?.Name ?? header.DisplayName,
                $"Status: {transfer.Status}, Type: {transfer.TransferType}"));
        }

        // Sort chronologically descending, apply limit
        return events
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToList();
    }

    // Epic 6.5: Campaign performance
    public async Task<CampaignPerformanceResult?> GetCampaignPerformanceAsync(
        Guid planId, List<Guid> brandIds, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || !brandIds.Contains(plan.BrandId))
            return null;

        var allDetails = (await _detailRepository.FindAsync(d => d.ParentId == planId, cancellationToken)).ToList();
        var total = allDetails.Count;
        var distributed = allDetails.Count(d => d.MemberId != null);
        var redeemed = allDetails.Count(d => d.UsageStatus == UsageStatus.Complete);
        var rate = total > 0 ? (decimal)redeemed / total : 0;

        // Epic 6.5: Per-outlet breakdown from completed vouchers with LockedOutletId
        var outletBreakdown = new List<OutletPerformance>();
        var redeemedDetails = allDetails.Where(d => d.UsageStatus == UsageStatus.Complete && d.LockedOutletId.HasValue).ToList();
        var grouped = redeemedDetails.GroupBy(d => d.LockedOutletId!.Value);
        foreach (var group in grouped)
        {
            var outlet = await _outletRepository.GetByIdAsync(group.Key, cancellationToken);
            outletBreakdown.Add(new OutletPerformance(
                group.Key,
                outlet?.Name ?? "Unknown",
                group.Count(),
                group.Count() * plan.FaceValue));
        }

        return new CampaignPerformanceResult(
            planId,
            plan.DisplayName,
            total,
            distributed,
            redeemed,
            rate,
            outletBreakdown);
    }

    /// <summary>
    /// True when a pasted token is an email address rather than a phone number.
    /// </summary>
    private static bool IsEmailToken(string token) => _emailRegex.IsMatch(token);

    /// <summary>
    /// Resolves email from the phoneToEmail mapping (normalized phone → email).
    /// Used by Integration API (Story 6.2) to supply member emails from the partner payload.
    /// Future: Zalo-based notification will use phone number directly instead of email.
    /// </summary>
    private static string? ResolveEmail(string phone, IReadOnlyDictionary<string, string>? phoneToEmail)
    {
        if (phoneToEmail == null || phoneToEmail.Count == 0)
            return null;

        // Try exact match first, then normalized phone
        if (phoneToEmail.TryGetValue(phone, out var email))
            return email;

        var normalized = Customer.NormalizePhoneNumber(phone);
        if (!string.IsNullOrEmpty(normalized) && phoneToEmail.TryGetValue(normalized, out email))
            return email;

        return null;
    }
}

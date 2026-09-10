using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class PosService : IPosService
{
    public const int LockTtlMinutes = 10;

    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IRepository<Outlet> _outletRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly IVoucherCodeService _codeService;
    private readonly IVoucherLockRepository _lockRepository;
    private readonly ISettlementService _settlementService;
    private readonly IVoucherEventPublisher _eventPublisher;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly IBrandCustomerRepository _brandCustomerRepository;

    public PosService(
        IRepository<VoucherPlanDetail> detailRepository,
        IVoucherPlanRepository planRepository,
        IRepository<Outlet> outletRepository,
        IBrandRepository brandRepository,
        IVoucherCodeService codeService,
        IVoucherLockRepository lockRepository,
        ISettlementService settlementService,
        IVoucherEventPublisher eventPublisher,
        IMemberAccountRepository memberRepository,
        IBrandCustomerRepository brandCustomerRepository)
    {
        _detailRepository = detailRepository;
        _planRepository = planRepository;
        _outletRepository = outletRepository;
        _brandRepository = brandRepository;
        _codeService = codeService;
        _lockRepository = lockRepository;
        _settlementService = settlementService;
        _eventPublisher = eventPublisher;
        _memberRepository = memberRepository;
        _brandCustomerRepository = brandCustomerRepository;
    }

    public async Task<PosVerifyResult> VerifyAsync(
        string voucherCode,
        Guid outletId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ValidateCoreAsync(voucherCode, outletId, allowInUseSelfRecovery: false, cancellationToken);
        if (ctx.Failure != null)
            return new PosVerifyResult("Invalid", ctx.Failure, null);

        return new PosVerifyResult("Valid", null, await BuildInfoAsync(ctx, cancellationToken));
    }

    public async Task<PosLockResult> LockAsync(
        string voucherCode,
        Guid outletId,
        string billNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(billNumber))
            return new PosLockResult("Invalid", "MissingBillNumber", null, null);

        // Defensive: release expired locks before checking status
        var cutoff = DateTime.UtcNow.AddMinutes(-LockTtlMinutes);
        await _lockRepository.ReleaseExpiredLocksAsync(cutoff, cancellationToken);

        var ctx = await ValidateCoreAsync(voucherCode, outletId, allowInUseSelfRecovery: true, cancellationToken);
        if (ctx.Failure != null)
        {
            // AC2 / AC4: differentiate AlreadyInUse vs forged/expired
            if (ctx.Failure == "AlreadyUsed" && ctx.Detail != null && ctx.Detail.UsageStatus == UsageStatus.InUse)
            {
                // AC4 idempotency: same outlet+bill → return existing lock
                if (ctx.Detail.LockedOutletId == outletId
                    && ctx.Detail.BillNumber == billNumber
                    && ctx.Detail.LockId != null)
                {
                    return new PosLockResult("Locked", null, ctx.Detail.LockId, await BuildInfoAsync(ctx, cancellationToken));
                }
                return new PosLockResult("AlreadyInUse", "AlreadyInUse", null, null);
            }
            return new PosLockResult("Invalid", ctx.Failure, null, null);
        }

        // Atomic conditional update Pending → InUse
        var now = DateTime.UtcNow;
        var lockId = await _lockRepository.TryAcquireLockAsync(ctx.Detail!.Id, outletId, billNumber, now, cancellationToken);
        if (lockId == null)
        {
            // Race: someone else just locked it. Re-read for idempotency.
            var current = await _lockRepository.FindByIdAsync(ctx.Detail.Id, cancellationToken);
            if (current != null
                && current.UsageStatus == UsageStatus.InUse
                && current.LockedOutletId == outletId
                && current.BillNumber == billNumber
                && current.LockId != null)
            {
                return new PosLockResult("Locked", null, current.LockId, await BuildInfoAsync(ctx, cancellationToken));
            }
            return new PosLockResult("AlreadyInUse", "AlreadyInUse", null, null);
        }

        return new PosLockResult("Locked", null, lockId, await BuildInfoAsync(ctx, cancellationToken));
    }

    public async Task<PosCommitResult> CommitAsync(
        Guid lockId,
        string transactionId,
        decimal amountUsed,
        Guid outletId,
        string? posNo = null,
        string? operatorId = null,
        CancellationToken cancellationToken = default)
    {
        if (lockId == Guid.Empty)
            return new PosCommitResult("Invalid", null, "BadRequest");
        if (string.IsNullOrWhiteSpace(transactionId))
            return new PosCommitResult("Invalid", null, "MissingTransactionId");
        if (amountUsed < 0)
            return new PosCommitResult("Invalid", null, "InvalidAmount");

        // AC5 idempotency: short-circuit on duplicate transactionId.
        var existingUsage = await _lockRepository.FindUsageByTransactionIdAsync(transactionId, cancellationToken);
        if (existingUsage != null)
        {
            // If the original commit was for the same voucher, treat as success-replay.
            return new PosCommitResult("Success", "Voucher already committed", null);
        }

        // Epic 7.1: Resolve settlement attribution from plan header + outlet brand.
        Guid? sponsorBrandId = null;
        Guid? redeemBrandId = null;
        Guid issuingBrandId = Guid.Empty;
        decimal faceValue = 0;
        var lockedDetail = await _lockRepository.FindByLockIdAsync(lockId, cancellationToken);
        if (lockedDetail != null)
        {
            var plan = await _planRepository.GetByIdWithOutletsAsync(lockedDetail.ParentId, cancellationToken);
            sponsorBrandId = plan?.SponsorBrandId;
            issuingBrandId = plan?.BrandId ?? Guid.Empty;
            faceValue = plan?.FaceValue ?? 0;

            var outlet = await _outletRepository.GetByIdAsync(outletId, cancellationToken);
            redeemBrandId = outlet?.BrandId;
        }

        var now = DateTime.UtcNow;
        var expiryCutoff = now.AddMinutes(-LockTtlMinutes);

        var outcome = await _lockRepository.CommitAsync(
            lockId, transactionId, amountUsed, outletId, now, expiryCutoff,
            sponsorBrandId, redeemBrandId, posNo, operatorId, cancellationToken);

        // Epic 7.2: Create settlement entry for cross-tenant redemptions.
        if (outcome == CommitOutcome.Success
            && sponsorBrandId.HasValue
            && sponsorBrandId.Value != redeemBrandId)
        {
            var usage = await _lockRepository.FindUsageByTransactionIdAsync(transactionId, cancellationToken);
            if (usage != null)
            {
                await _settlementService.CreateSettlementEntryAsync(
                    usage, issuingBrandId, faceValue, cancellationToken);
            }
        }

        // Customer-model redesign: redeeming links the member's customer to the issuing brand.
        // O1 (customer-action-matrix): deliberately NO customer-status check — held vouchers
        // stay redeemable even when the customer is blocked/blacklisted (grandfathering).
        if (outcome == CommitOutcome.Success
            && lockedDetail?.MemberId != null
            && issuingBrandId != Guid.Empty)
        {
            var redeemingMember = await _memberRepository.GetByIdAsync(lockedDetail.MemberId.Value, cancellationToken);
            if (redeemingMember != null)
            {
                await _brandCustomerRepository.EnsureAsync(
                    issuingBrandId, redeemingMember.CustomerId, BrandCustomerSource.Redemption, null, cancellationToken);
            }
        }

        // Epic 6.4: Publish webhook event on successful redemption.
        if (outcome == CommitOutcome.Success && lockedDetail != null)
        {
            try
            {
                await _eventPublisher.PublishAsync(
                    "voucher.redeemed",
                    lockedDetail.Id,
                    null, // member phone resolved by webhook consumer if needed
                    issuingBrandId,
                    new { transactionId, amountUsed, outletId, lockedDetail.SerialNo },
                    cancellationToken);
            }
            catch
            {
                // Best-effort: event publishing errors must not fail the redemption.
            }
        }

        return outcome switch
        {
            CommitOutcome.Success => new PosCommitResult("Success", "Voucher completed", null),
            CommitOutcome.AlreadyComplete => new PosCommitResult("AlreadyComplete", null, "AlreadyComplete"),
            CommitOutcome.LockExpired => new PosCommitResult("LockExpired", null, "LockExpired"),
            CommitOutcome.LockNotFound => new PosCommitResult("LockExpired", null, "LockNotFound"),
            _ => new PosCommitResult("Invalid", null, "Unknown")
        };
    }

    public async Task<PosRollbackResult> RollbackAsync(
        Guid lockId,
        CancellationToken cancellationToken = default)
    {
        if (lockId == Guid.Empty)
            return new PosRollbackResult("Invalid", null, "BadRequest");

        var outcome = await _lockRepository.RollbackAsync(lockId, cancellationToken);

        return outcome switch
        {
            RollbackOutcome.Success => new PosRollbackResult("Success", "Voucher released", null),
            // AC3 + AC4: expired/idempotent → 200 AlreadyReleased
            RollbackOutcome.AlreadyReleased => new PosRollbackResult("AlreadyReleased", "Voucher already released", null),
            RollbackOutcome.LockNotFound => new PosRollbackResult("AlreadyReleased", "Voucher already released", null),
            // AC2: 409 AlreadyCompleted
            RollbackOutcome.AlreadyComplete => new PosRollbackResult("AlreadyCompleted", null, "AlreadyCompleted"),
            _ => new PosRollbackResult("Invalid", null, "Unknown")
        };
    }

    // ----- helpers -----

    private async Task<ValidationContext> ValidateCoreAsync(
        string voucherCode,
        Guid outletId,
        bool allowInUseSelfRecovery,
        CancellationToken cancellationToken)
    {
        var ctx = new ValidationContext();

        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            ctx.Failure = "InvalidCodeFormat";
            return ctx;
        }

        // A serial number (VC-...) is not a scannable code: the POS contract takes the
        // short-lived signed token minted per voucher, so anything non-token-shaped is
        // reported as a format problem, not as counterfeiting.
        if (!_codeService.TryExtractVoucherId(voucherCode, out var voucherId))
        {
            ctx.Failure = "InvalidCodeFormat";
            return ctx;
        }

        var detail = await _detailRepository.GetByIdAsync(voucherId, cancellationToken);
        if (detail == null)
        {
            ctx.Failure = "Forged";
            return ctx;
        }
        ctx.Detail = detail;

        var check = _codeService.CheckCode(voucherCode, detail.VoucherCodeSecret, out var validatedId);
        if (check == VoucherCodeCheck.Expired)
        {
            ctx.Failure = "CodeExpired";
            return ctx;
        }
        if (check != VoucherCodeCheck.Valid || validatedId != voucherId)
        {
            ctx.Failure = "Forged";
            return ctx;
        }

        if (detail.UsageStatus != UsageStatus.Pending)
        {
            // For Lock idempotency, callers want the detail context preserved.
            ctx.Failure = detail.UsageStatus == UsageStatus.InUse ? "AlreadyUsed" : "AlreadyUsed";
            if (!allowInUseSelfRecovery) return ctx;
            // proceed enough to load plan/brand for response info
        }

        // Story 5-1: reject vouchers that are pending a peer-to-peer transfer
        if (IsTransferLocked(detail))
        {
            ctx.Failure = "TransferPending";
            return ctx;
        }

        var plan = await _planRepository.GetByIdWithOutletsAsync(detail.ParentId, cancellationToken);
        if (plan == null)
        {
            ctx.Failure = "Forged";
            return ctx;
        }
        ctx.Plan = plan;

        var outlet = await _outletRepository.GetByIdAsync(outletId, cancellationToken);
        if (outlet == null || outlet.BrandId != plan.BrandId)
        {
            ctx.Failure = "OutletNotAuthorized";
            return ctx;
        }
        // Epic 3: hierarchy-aware scope check. Empty Outlets = whole brand (same-brand gate
        // above already ensures the outlet belongs to the plan's brand). Non-empty Outlets
        // = only the explicitly listed outlets.
        if (!plan.Scope.CoversOutlet(outletId))
        {
            ctx.Failure = "OutletNotAuthorized";
            return ctx;
        }

        var now = DateTime.UtcNow;
        if (now > plan.ExpiryDate || (plan.ValidTo.HasValue && now > plan.ValidTo.Value))
        {
            ctx.Failure = "Expired";
            return ctx;
        }
        if ((plan.ValidFrom.HasValue && now < plan.ValidFrom.Value) || now < plan.PublishDate)
        {
            ctx.Failure = "NotYetValid";
            return ctx;
        }

        var brand = await _brandRepository.GetByIdAsync(plan.BrandId, cancellationToken);
        ctx.BrandName = brand?.Name ?? string.Empty;
        // Preserve "AlreadyUsed" failure if it was set above
        return ctx;
    }

    private static bool IsTransferLocked(VoucherPlanDetail detail)
    {
        if (detail.TransferLockId == null || detail.TransferLockedAt == null)
            return false;

        // Transfer locks expire after 7 days (matching VoucherTransferService.TransferExpiryDays)
        return detail.TransferLockedAt.Value.AddDays(VoucherTransferService.TransferExpiryDays) > DateTime.UtcNow;
    }

    private async Task<PosVoucherInfo> BuildInfoAsync(ValidationContext ctx, CancellationToken cancellationToken)
    {
        var plan = ctx.Plan!;

        // Resolve the explicit outlet names of the scope so the POS can show the cashier
        // exactly which stores redeem this voucher. Empty scope = whole brand (no list).
        var outletNames = new List<string>();
        if (plan.Scope.Outlets.Count > 0)
        {
            var outlets = await _outletRepository.FindAsync(o => plan.Scope.Outlets.Contains(o.Id), cancellationToken);
            outletNames = outlets.Select(o => o.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var scopeSummary = plan.Scope.Outlets.Count == 0
            ? (string.IsNullOrWhiteSpace(ctx.BrandName) ? "All stores of the brand" : $"All stores of {ctx.BrandName}")
            : $"{plan.Scope.Outlets.Count} selected store(s)";

        return new PosVoucherInfo(
            FaceValue: plan.FaceValue,
            ValueType: plan.ValueType.ToString(),
            ExpiryDate: plan.ExpiryDate,
            BrandName: ctx.BrandName,
            SerialNo: ctx.Detail!.SerialNo,
            ScopeSummary: scopeSummary,
            ApplicableOutlets: outletNames
        );
    }

    private class ValidationContext
    {
        public VoucherPlanDetail? Detail;
        public VoucherPlanHeader? Plan;
        public string BrandName = string.Empty;
        public string? Failure;
    }
}

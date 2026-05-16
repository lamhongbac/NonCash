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

    public PosService(
        IRepository<VoucherPlanDetail> detailRepository,
        IVoucherPlanRepository planRepository,
        IRepository<Outlet> outletRepository,
        IBrandRepository brandRepository,
        IVoucherCodeService codeService,
        IVoucherLockRepository lockRepository)
    {
        _detailRepository = detailRepository;
        _planRepository = planRepository;
        _outletRepository = outletRepository;
        _brandRepository = brandRepository;
        _codeService = codeService;
        _lockRepository = lockRepository;
    }

    public async Task<PosVerifyResult> VerifyAsync(
        string voucherCode,
        Guid outletId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ValidateCoreAsync(voucherCode, outletId, allowInUseSelfRecovery: false, cancellationToken);
        if (ctx.Failure != null)
            return new PosVerifyResult("Invalid", ctx.Failure, null);

        return new PosVerifyResult("Valid", null, BuildInfo(ctx));
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
                    return new PosLockResult("Locked", null, ctx.Detail.LockId, BuildInfo(ctx));
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
                return new PosLockResult("Locked", null, current.LockId, BuildInfo(ctx));
            }
            return new PosLockResult("AlreadyInUse", "AlreadyInUse", null, null);
        }

        return new PosLockResult("Locked", null, lockId, BuildInfo(ctx));
    }

    public async Task<PosCommitResult> CommitAsync(
        Guid lockId,
        string transactionId,
        decimal amountUsed,
        Guid outletId,
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

        var now = DateTime.UtcNow;
        var expiryCutoff = now.AddMinutes(-LockTtlMinutes);

        var outcome = await _lockRepository.CommitAsync(
            lockId, transactionId, amountUsed, outletId, now, expiryCutoff, cancellationToken);

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
            ctx.Failure = "Forged";
            return ctx;
        }

        if (!_codeService.TryExtractVoucherId(voucherCode, out var voucherId))
        {
            ctx.Failure = "Forged";
            return ctx;
        }

        var detail = await _detailRepository.GetByIdAsync(voucherId, cancellationToken);
        if (detail == null)
        {
            ctx.Failure = "Forged";
            return ctx;
        }
        ctx.Detail = detail;

        var validatedId = _codeService.ValidateCode(voucherCode, detail.VoucherCodeSecret);
        if (validatedId == null || validatedId != voucherId)
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
        if (!plan.PlanOutlets.Any(po => po.OutletId == outletId))
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

    private static PosVoucherInfo BuildInfo(ValidationContext ctx)
    {
        return new PosVoucherInfo(
            FaceValue: ctx.Plan!.FaceValue,
            ValueType: ctx.Plan.ValueType.ToString(),
            ExpiryDate: ctx.Plan.ExpiryDate,
            BrandName: ctx.BrandName,
            SerialNo: ctx.Detail!.SerialNo
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

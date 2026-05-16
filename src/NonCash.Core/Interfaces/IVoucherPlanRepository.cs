using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IVoucherPlanRepository : IRepository<VoucherPlanHeader>
{
    Task<IReadOnlyList<VoucherPlanHeader>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherPlanHeader>> ListByBrandAndStatusAsync(Guid brandId, ApprovalStatus status, CancellationToken cancellationToken = default);
    Task<VoucherPlanHeader?> GetByIdWithOutletsAsync(Guid id, CancellationToken cancellationToken = default);
}

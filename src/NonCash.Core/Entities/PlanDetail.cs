using System;
using NonCash.Core.Entities.Base;
using NonCash.Shared.Enums;

namespace NonCash.Core.Entities
{
    public class PlanDetail : BaseEntity
    {
        // Link to Voucher Plan header table
        public Guid ProductionPlanId { get; set; }
        public ProductionPlan ProductionPlan { get; set; } = null!;
        
        // SerialNo duy nhất
        public string SerialNo { get; set; } = string.Empty;
        
        // Mã thực tế để check (có thể thay đổi dạng TOTP/JWT nếu cần)
        public string DynamicVoucherCode { get; set; } = string.Empty;
        
        // Mã khách hàng/Organization sở hữu
        public Guid? MemberId { get; set; }
        public Member? Member { get; set; }
        
        // Trạng thái sử dụng (Pending, In-Use, Complete)
        public VoucherStatus Status { get; set; } = VoucherStatus.Pending;
        
        public DateTime? UsedDate { get; set; }
    }
}

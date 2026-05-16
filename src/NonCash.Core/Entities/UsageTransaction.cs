using System;
using NonCash.Core.Entities.Base;

namespace NonCash.Core.Entities
{
    public class UsageTransaction : BaseEntity
    {
        // Có thể null nếu hệ thống tách rời, nhưng tốt nhất nên link
        public Guid PlanDetailId { get; set; }
        public PlanDetail PlanDetail { get; set; } = null!;
        
        public Guid PosSystemId { get; set; }
        
        public decimal UsedAmount { get; set; }
        
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        
        // Mã giao dịch ở hệ thống lưu trữ POS
        public string PosReferenceNumber { get; set; } = string.Empty;
    }
}

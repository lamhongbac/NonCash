using System;
using NonCash.Core.Entities.Base;
using NonCash.Shared.Enums;

namespace NonCash.Core.Entities
{
    public class ApprovalTransaction : BaseEntity
    {
        public Guid ProductionPlanId { get; set; }
        public ProductionPlan ProductionPlan { get; set; } = null!;
        
        // Người duyệt
        public Guid ReviewerId { get; set; }
        
        public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
        
        public string ReviewNotes { get; set; } = string.Empty;
        
        public ApprovalStatus Status { get; set; }
        
        public DateTime? PublishDate { get; set; } // Ngày điều chỉnh nếu có
    }
}

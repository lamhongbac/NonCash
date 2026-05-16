using System;
using System.Collections.Generic;
using NonCash.Core.Entities.Base;
using NonCash.Shared.Enums;

namespace NonCash.Core.Entities
{
    public class ProductionPlan : BaseEntity
    {
        // Tên kế hoạch hoặc định danh đợt
        public string PlanName { get; set; } = string.Empty;
        
        // BrandID: Voucher phát hành bởi bên nào
        public Guid BusinessId { get; set; }
        public Business Business { get; set; } = null!;
        
        // Loại voucher: complimentary voucher và Gift voucher
        public VoucherType VoucherType { get; set; }
        
        // Hình thức hiển thị
        public string ImageUrl { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        
        // Term and condition / điều khoản sử dụng
        public string TermsAndConditions { get; set; } = string.Empty;
        
        // Hình thức giá trị: Value vs Percentage
        public VoucherValueType ValueType { get; set; }
        
        // Mệnh giá: giá trị sử dụng
        public decimal FaceValue { get; set; }
        
        // NetValue: giá trị tham khảo khi bán
        public decimal NetValue { get; set; }
        
        // Price: giá bán thực tế
        public decimal Price { get; set; }
        
        // Phủ quyết ngày nếu ExpiryDate được set
        public DateTime? ExpiryDate { get; set; }
        
        // Phân phát từ ngày
        public DateTime? PublishDate { get; set; }
        
        // Phạm vi thời gian sử dụng
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        
        // Phạm vi bán hàng: danh sách các thương hiệu & điểm bán (dạng JSON hoặc chuỗi phụ)
        public string AllowedLocations { get; set; } = string.Empty;
        
        // Số lượng dự kiến xuất bản
        public int PlannedQuantity { get; set; }
        
        // Ngân sách dự kiến
        public decimal TotalBudget { get; set; }
        
        // Targets
        public int TargetDistributionQuantity { get; set; }
        public int TargetUsageQuantity { get; set; }
        
        // Status duyệt
        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
        
        // Navigation Properties
        public ICollection<PlanDetail> PlanDetails { get; set; } = new List<PlanDetail>();
        public ICollection<ApprovalTransaction> ApprovalTransactions { get; set; } = new List<ApprovalTransaction>();
    }
}

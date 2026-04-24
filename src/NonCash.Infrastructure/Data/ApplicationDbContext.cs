using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Member> Members { get; set; } = null!;
        public DbSet<Business> Businesses { get; set; } = null!;
        public DbSet<ProductionPlan> ProductionPlans { get; set; } = null!;
        public DbSet<PlanDetail> PlanDetails { get; set; } = null!;
        public DbSet<ApprovalTransaction> ApprovalTransactions { get; set; } = null!;
        public DbSet<UsageTransaction> UsageTransactions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình các ràng buộc (Constraints/FluentAPI) tại đây
            // modelBuilder.Entity<PlanDetail>()
            //   .HasOne(p => p.ProductionPlan)
            //   .WithMany(b => b.PlanDetails)
            //   .HasForeignKey(p => p.ProductionPlanId);

            // Các index để truy vấn nhanh
            modelBuilder.Entity<PlanDetail>()
                .HasIndex(v => v.SerialNo)
                .IsUnique();

            modelBuilder.Entity<Business>()
                .HasIndex(b => b.TaxCode)
                .IsUnique();
        }
    }
}

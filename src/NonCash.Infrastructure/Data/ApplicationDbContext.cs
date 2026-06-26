using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using System.Reflection;

namespace NonCash.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<MemberAccount> MemberAccounts => Set<MemberAccount>();
    public DbSet<BrandRegistrationRequest> BrandRegistrationRequests => Set<BrandRegistrationRequest>();
    public DbSet<VoucherPlanHeader> VoucherPlanHeaders => Set<VoucherPlanHeader>();
    public DbSet<PlanOutlet> PlanOutlets => Set<PlanOutlet>();
    public DbSet<VoucherPlanDetail> VoucherPlanDetails => Set<VoucherPlanDetail>();
    public DbSet<VoucherTransfer> VoucherTransfers => Set<VoucherTransfer>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    if (entry.Entity.Id == Guid.Empty)
                    {
                        entry.Entity.Id = Guid.NewGuid();
                    }
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return string.Concat(
            input.Select((c, i) =>
                i > 0 && char.IsUpper(c)
                    ? "_" + char.ToLowerInvariant(c)
                    : char.ToLowerInvariant(c).ToString()));
    }
}

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("NONCASH_CONNECTION_STRING")
            ?? "Host=localhost;Database=noncash;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}

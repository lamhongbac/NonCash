using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class ContractTemplateConfiguration : IEntityTypeConfiguration<ContractTemplate>
{
    public void Configure(EntityTypeBuilder<ContractTemplate> builder)
    {
        builder.ToTable("contract_templates");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.HtmlTemplate).IsRequired();

        builder.Property(p => p.IsActive).HasDefaultValue(true);

        // Only one default template at a time.
        builder.HasIndex(p => p.IsDefault)
            .HasDatabaseName("IX_contract_templates_is_default")
            .IsUnique()
            .HasFilter("is_default = true");
    }
}

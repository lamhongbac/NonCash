using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class WelcomeGrantPolicyTemplateConfiguration : IEntityTypeConfiguration<WelcomeGrantPolicyTemplate>
{
    public void Configure(EntityTypeBuilder<WelcomeGrantPolicyTemplate> builder)
    {
        builder.ToTable("welcome_grant_policy_templates");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

        // Only one default template at a time.
        builder.HasIndex(p => p.IsDefault)
            .HasDatabaseName("IX_welcome_grant_policy_templates_is_default")
            .IsUnique()
            .HasFilter("is_default = true");
    }
}

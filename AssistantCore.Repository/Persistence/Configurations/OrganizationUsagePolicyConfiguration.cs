using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class OrganizationUsagePolicyConfiguration : IEntityTypeConfiguration<OrganizationUsagePolicy>
{
    public void Configure(EntityTypeBuilder<OrganizationUsagePolicy> builder)
    {
        builder.ToTable("OrganizationUsagePolicy");
        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.Id).ValueGeneratedNever();
        builder.Property(policy => policy.OrganizationId).IsRequired();
        builder.Property(policy => policy.Version).IsRequired();
        builder.Property(policy => policy.MonthlyTokenLimit).IsRequired();

        builder.Property(policy => policy.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(policy => policy.ActorId).IsRequired();

        builder.HasOne(policy => policy.Organization)
            .WithMany()
            .HasForeignKey(policy => policy.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(policy => new { policy.OrganizationId, policy.Version })
            .IsUnique()
            .HasDatabaseName("IX_OrganizationUsagePolicy_OrganizationId_Version");

        builder.HasIndex(policy => new { policy.OrganizationId, policy.EffectiveAt })
            .IsUnique()
            .HasDatabaseName("IX_OrganizationUsagePolicy_OrganizationId_EffectiveAt");
    }
}

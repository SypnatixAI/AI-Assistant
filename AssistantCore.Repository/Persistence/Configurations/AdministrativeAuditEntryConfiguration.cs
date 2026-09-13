using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class AdministrativeAuditEntryConfiguration : IEntityTypeConfiguration<AdministrativeAuditEntry>
{
    public void Configure(EntityTypeBuilder<AdministrativeAuditEntry> builder)
    {
        builder.ToTable("AdministrativeAuditEntry");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.OrganizationId).IsRequired();

        builder.Property(entry => entry.ActorType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.ActorId).IsRequired();

        builder.Property(entry => entry.Action)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.TargetType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.TargetId).IsRequired();

        builder.Property(entry => entry.OldValues)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(entry => entry.NewValues)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(entry => entry.CorrelationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne(entry => entry.Organization)
            .WithMany()
            .HasForeignKey(entry => entry.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entry => new { entry.OrganizationId, entry.OccurredAt })
            .HasDatabaseName("IX_AdministrativeAuditEntry_OrganizationId_OccurredAt");

        builder.HasIndex(entry => new { entry.TargetType, entry.TargetId })
            .HasDatabaseName("IX_AdministrativeAuditEntry_TargetType_TargetId");
    }
}

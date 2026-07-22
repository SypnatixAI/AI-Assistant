using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMember");

        builder.HasKey(member => member.Id);

        builder.Property(member => member.Id)
            .ValueGeneratedNever();

        builder.Property(member => member.OrganizationId)
            .IsRequired();

        builder.Property(member => member.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(member => member.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(member => member.MicrosoftIdentifier)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(member => member.Role)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(member => member.Status)
            .HasConversion(
                status => status == RecordStatus.Active ? "Actif" : "Inactif",
                value => value == "Actif" ? RecordStatus.Active : RecordStatus.Inactive)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(member => new { member.OrganizationId, member.Email })
            .IsUnique();
    }
}

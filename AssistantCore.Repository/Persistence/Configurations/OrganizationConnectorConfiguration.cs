using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class OrganizationConnectorConfiguration : IEntityTypeConfiguration<OrganizationConnector>
{
    public void Configure(EntityTypeBuilder<OrganizationConnector> builder)
    {
        builder.ToTable("OrganizationConnector");

        builder.HasKey(connector => connector.Id);

        builder.Property(connector => connector.Id)
            .ValueGeneratedNever();

        builder.Property(connector => connector.OrganizationId)
            .IsRequired();

        builder.Property(connector => connector.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(connector => connector.Status)
            .HasConversion(
                status => status == RecordStatus.Active ? "Actif" : "Inactif",
                value => value == "Actif" ? RecordStatus.Active : RecordStatus.Inactive)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(connector => connector.IsConfigured)
            .IsRequired();

        builder.HasMany(connector => connector.Sources)
            .WithOne(source => source.OrganizationConnector)
            .HasForeignKey(source => source.OrganizationConnectorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(connector => connector.Microsoft365Connection)
            .WithOne(connection => connection.OrganizationConnector)
            .HasForeignKey<Microsoft365Connection>(connection => connection.OrganizationConnectorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(connector => new { connector.OrganizationId, connector.Type })
            .IsUnique();
    }
}

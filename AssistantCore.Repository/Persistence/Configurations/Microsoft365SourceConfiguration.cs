using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class Microsoft365SourceConfiguration : IEntityTypeConfiguration<Microsoft365Source>
{
    public void Configure(EntityTypeBuilder<Microsoft365Source> builder)
    {
        builder.UseTptMappingStrategy();
        builder.ToTable("Microsoft365Source");
        builder.HasKey(source => source.Id);

        builder.Property(source => source.Id).ValueGeneratedNever();
        builder.Property(source => source.Kind).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(source => source.ExternalResourceId).HasMaxLength(400).IsRequired();
        builder.Property(source => source.ParentExternalResourceId).HasMaxLength(400);
        builder.Property(source => source.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(source => source.WebUrl).HasMaxLength(2048);
        builder.Property(source => source.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(source => source.StatusBeforeUnavailable).HasConversion<string>().HasMaxLength(30);
        builder.Property(source => source.DeltaLink);
        builder.Property(source => source.LastErrorCode).HasMaxLength(100);

        builder.HasOne(source => source.Microsoft365Connection)
            .WithMany(connection => connection.Sources)
            .HasForeignKey(source => source.Microsoft365ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(source => new
        {
            source.Microsoft365ConnectionId,
            source.Kind,
            source.ExternalResourceId
        }).IsUnique();
    }
}

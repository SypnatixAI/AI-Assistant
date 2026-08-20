using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class Microsoft365SynchronizationConfiguration : IEntityTypeConfiguration<Microsoft365Synchronization>
{
    public void Configure(EntityTypeBuilder<Microsoft365Synchronization> builder)
    {
        builder.ToTable("Microsoft365Synchronization");
        builder.HasKey(synchronization => synchronization.Id);

        builder.Property(synchronization => synchronization.Id).ValueGeneratedNever();
        builder.Property(synchronization => synchronization.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(synchronization => synchronization.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(synchronization => synchronization.AttemptCount).IsRequired();
        builder.Property(synchronization => synchronization.LastErrorCode).HasMaxLength(100);

        builder.HasOne(synchronization => synchronization.Microsoft365Source)
            .WithMany(source => source.Synchronizations)
            .HasForeignKey(synchronization => synchronization.Microsoft365SourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(synchronization => new { synchronization.Microsoft365SourceId, synchronization.Status });
        builder.HasIndex(synchronization => synchronization.Microsoft365SourceId)
            .IsUnique()
            .HasFilter($"[Status] = '{Microsoft365SynchronizationStatus.Running}'");
    }
}

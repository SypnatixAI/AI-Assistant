using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class MessageSourceConfiguration : IEntityTypeConfiguration<MessageSource>
{
    public void Configure(EntityTypeBuilder<MessageSource> builder)
    {
        builder.ToTable("MessageSource");

        builder.HasKey(source => source.Id);

        builder.Property(source => source.Id)
            .ValueGeneratedNever();

        builder.Property(source => source.MessageId)
            .IsRequired();

        builder.Property(source => source.SourceType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(source => source.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(source => source.Reference)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(source => source.Url)
            .HasMaxLength(2048);

        builder.Property(source => source.SourceDate)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(source => source.MessageId);
    }
}

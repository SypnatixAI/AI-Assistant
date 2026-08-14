using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class MessageWarningConfiguration : IEntityTypeConfiguration<MessageWarning>
{
    public void Configure(EntityTypeBuilder<MessageWarning> builder)
    {
        builder.ToTable("MessageWarning");

        builder.HasKey(warning => warning.Id);

        builder.Property(warning => warning.Id)
            .ValueGeneratedNever();

        builder.Property(warning => warning.MessageId)
            .IsRequired();

        builder.Property(warning => warning.Content)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(warning => warning.MessageId);
    }
}

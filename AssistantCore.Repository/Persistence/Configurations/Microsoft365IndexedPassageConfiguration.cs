using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class Microsoft365IndexedPassageConfiguration
    : IEntityTypeConfiguration<Microsoft365IndexedPassage>
{
    public void Configure(EntityTypeBuilder<Microsoft365IndexedPassage> builder)
    {
        builder.ToTable("Microsoft365IndexedPassage");
        builder.HasKey(passage => passage.Id);
        builder.Property(passage => passage.Id).ValueGeneratedNever();
        builder.Property(passage => passage.ChunkId).HasMaxLength(400).IsRequired();
        builder.HasOne(passage => passage.Microsoft365IndexedContent)
            .WithMany(content => content.Passages)
            .HasForeignKey(passage => passage.Microsoft365IndexedContentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(passage => passage.ChunkId).IsUnique();
    }
}

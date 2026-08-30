using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class ConversationPurgeRequestConfiguration
    : IEntityTypeConfiguration<ConversationPurgeRequest>
{
    public void Configure(EntityTypeBuilder<ConversationPurgeRequest> builder)
    {
        builder.ToTable("ConversationPurgeRequest");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .ValueGeneratedNever();

        builder.Property(request => request.ConversationId)
            .IsRequired();

        builder.Property(request => request.OrganizationId)
            .IsRequired();

        builder.Property(request => request.RequestedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(request => request.PurgeAfter)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(request => request.Conversation)
            .WithMany()
            .HasForeignKey(request => request.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(request => request.ConversationId)
            .IsUnique();

        builder.HasIndex(request => new { request.Status, request.PurgeAfter });
    }
}

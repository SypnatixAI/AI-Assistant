using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class Microsoft365SubscriptionConfiguration : IEntityTypeConfiguration<Microsoft365Subscription>
{
    public void Configure(EntityTypeBuilder<Microsoft365Subscription> builder)
    {
        builder.ToTable("Microsoft365Subscription");
        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Id).ValueGeneratedNever();
        builder.Property(subscription => subscription.MicrosoftSubscriptionId).HasMaxLength(150).IsRequired();
        builder.Property(subscription => subscription.ProtectedClientState).HasMaxLength(2048).IsRequired();
        builder.Property(subscription => subscription.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(subscription => subscription.LastErrorCode).HasMaxLength(100);

        builder.HasOne(subscription => subscription.Microsoft365Source)
            .WithMany(source => source.Subscriptions)
            .HasForeignKey(subscription => subscription.Microsoft365SourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(subscription => subscription.MicrosoftSubscriptionId).IsUnique();
        builder.HasIndex(subscription => new { subscription.Microsoft365SourceId, subscription.Status });
    }
}

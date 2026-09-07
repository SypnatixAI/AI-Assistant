using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistantCore.Repository.Persistence.Configurations;

public sealed class TokenConsumptionConfiguration : IEntityTypeConfiguration<TokenConsumption>
{
    public void Configure(EntityTypeBuilder<TokenConsumption> builder)
    {
        builder.ToTable("TokenConsumption");
        builder.HasKey(consumption => consumption.Id);

        builder.Property(consumption => consumption.Id).ValueGeneratedNever();
        builder.Property(consumption => consumption.OrganizationId).IsRequired();
        builder.Property(consumption => consumption.AssistantMessageId).IsRequired();
        builder.Property(consumption => consumption.InputTokens).IsRequired();
        builder.Property(consumption => consumption.OutputTokens).IsRequired();
        builder.Property(consumption => consumption.TotalTokens).IsRequired();

        builder.HasOne(consumption => consumption.Organization)
            .WithMany()
            .HasForeignKey(consumption => consumption.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(consumption => consumption.AssistantMessage)
            .WithMany()
            .HasForeignKey(consumption => consumption.AssistantMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(consumption => consumption.AssistantMessageId)
            .IsUnique()
            .HasDatabaseName("IX_TokenConsumption_OneRecordPerMessage");
        builder.HasIndex(consumption => new { consumption.OrganizationId, consumption.PeriodStartsAt })
            .HasDatabaseName("IX_TokenConsumption_OrganizationId_PeriodStartsAt");
    }
}

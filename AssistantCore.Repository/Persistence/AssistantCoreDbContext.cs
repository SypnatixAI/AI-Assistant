using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Persistence;

public sealed class AssistantCoreDbContext(DbContextOptions<AssistantCoreDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<OrganizationConnector> OrganizationConnectors => Set<OrganizationConnector>();

    public DbSet<OrganizationConnectorSource> OrganizationConnectorSources => Set<OrganizationConnectorSource>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MessageSource> MessageSources => Set<MessageSource>();

    public DbSet<MessageWarning> MessageWarnings => Set<MessageWarning>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssistantCoreDbContext).Assembly);
    }
}

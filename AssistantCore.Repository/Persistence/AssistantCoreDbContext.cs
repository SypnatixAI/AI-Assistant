using AssistantCore.Repository.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Persistence;

public sealed class AssistantCoreDbContext(DbContextOptions<AssistantCoreDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<OrganizationConnector> OrganizationConnectors => Set<OrganizationConnector>();

    public DbSet<OrganizationConnectorSource> OrganizationConnectorSources => Set<OrganizationConnectorSource>();

    public DbSet<Microsoft365Connection> Microsoft365Connections => Set<Microsoft365Connection>();

    public DbSet<Microsoft365Source> Microsoft365Sources => Set<Microsoft365Source>();

    public DbSet<Microsoft365Site> Microsoft365Sites => Set<Microsoft365Site>();

    public DbSet<Microsoft365Drive> Microsoft365Drives => Set<Microsoft365Drive>();

    public DbSet<Microsoft365List> Microsoft365Lists => Set<Microsoft365List>();

    public DbSet<Microsoft365Subscription> Microsoft365Subscriptions => Set<Microsoft365Subscription>();

    public DbSet<Microsoft365Synchronization> Microsoft365Synchronizations => Set<Microsoft365Synchronization>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MessageSource> MessageSources => Set<MessageSource>();

    public DbSet<MessageWarning> MessageWarnings => Set<MessageWarning>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssistantCoreDbContext).Assembly);
    }
}

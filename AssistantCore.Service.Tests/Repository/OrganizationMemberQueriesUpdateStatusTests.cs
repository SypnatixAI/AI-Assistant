using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Queries;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class OrganizationMemberQueriesUpdateStatusTests
{
    [Fact]
    public async Task Given_AMatchingVersion_When_UpdateStatus_Then_PersistsChangesAndIncrementsVersion()
    {
        // Given
        var member = CreateMember(RecordStatus.Active, version: 3);
        await using var dbContext = CreateDbContext();
        dbContext.OrganizationMembers.Add(member);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var queries = new OrganizationMemberQueries(dbContext);

        // When
        var result = await queries.UpdateStatus(
            member.OrganizationId,
            member.Id,
            RecordStatus.Inactive,
            expectedVersion: 3,
            CancellationToken.None);

        // Then
        Assert.Equal(MemberUpdateStatus.Updated, result.Status);
        Assert.NotNull(result.Member);
        Assert.Equal(RecordStatus.Inactive, result.Member.Status);
        Assert.Equal(4, result.Member.Version);
        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.OrganizationMembers.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == member.Id);
        Assert.Equal(RecordStatus.Inactive, persisted.Status);
        Assert.Equal(4, persisted.Version);
    }

    [Fact]
    public async Task Given_AStaleVersion_When_UpdateStatus_Then_ReturnsVersionConflictWithoutWriting()
    {
        // Given
        var member = CreateMember(RecordStatus.Active, version: 5);
        await using var dbContext = CreateDbContext();
        dbContext.OrganizationMembers.Add(member);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var queries = new OrganizationMemberQueries(dbContext);

        // When
        var result = await queries.UpdateStatus(
            member.OrganizationId,
            member.Id,
            RecordStatus.Inactive,
            expectedVersion: 4,
            CancellationToken.None);

        // Then
        Assert.Equal(MemberUpdateStatus.VersionConflict, result.Status);
        Assert.Null(result.Member);
        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.OrganizationMembers.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == member.Id);
        Assert.Equal(RecordStatus.Active, persisted.Status);
        Assert.Equal(5, persisted.Version);
    }

    [Fact]
    public async Task Given_TheSameStatus_When_UpdateStatus_Then_ReturnsWithoutChangingVersion()
    {
        // Given
        var member = CreateMember(RecordStatus.Inactive, version: 2);
        await using var dbContext = CreateDbContext();
        dbContext.OrganizationMembers.Add(member);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var queries = new OrganizationMemberQueries(dbContext);

        // When
        var result = await queries.UpdateStatus(
            member.OrganizationId,
            member.Id,
            RecordStatus.Inactive,
            expectedVersion: null,
            CancellationToken.None);

        // Then
        Assert.Equal(MemberUpdateStatus.Updated, result.Status);
        Assert.Equal(2, result.Member!.Version);
        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.OrganizationMembers.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == member.Id);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task Given_NoExpectedVersion_When_UpdateStatus_Then_AppliesTheChange()
    {
        // Given
        var member = CreateMember(RecordStatus.Active, version: 1);
        await using var dbContext = CreateDbContext();
        dbContext.OrganizationMembers.Add(member);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var queries = new OrganizationMemberQueries(dbContext);

        // When
        var result = await queries.UpdateStatus(
            member.OrganizationId,
            member.Id,
            RecordStatus.Inactive,
            expectedVersion: null,
            CancellationToken.None);

        // Then
        Assert.Equal(MemberUpdateStatus.Updated, result.Status);
        Assert.Equal(2, result.Member!.Version);
    }

    [Fact]
    public async Task Given_AMemberOfAnotherOrganization_When_UpdateStatus_Then_ReturnsNotFound()
    {
        // Given
        var member = CreateMember(RecordStatus.Active, version: 1);
        await using var dbContext = CreateDbContext();
        dbContext.OrganizationMembers.Add(member);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var queries = new OrganizationMemberQueries(dbContext);

        // When
        var result = await queries.UpdateStatus(
            Guid.NewGuid(),
            member.Id,
            RecordStatus.Inactive,
            expectedVersion: null,
            CancellationToken.None);

        // Then
        Assert.Equal(MemberUpdateStatus.NotFound, result.Status);
    }

    private static OrganizationMember CreateMember(RecordStatus status, int version) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Name = "Target Member",
        Email = "target@example.com",
        IdentityProvider = IdentityProvider.MicrosoftEntraId,
        ExternalUserId = Guid.NewGuid().ToString(),
        Role = OrganizationRole.User,
        Status = status,
        Version = version
    };

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AssistantCoreDbContext(options);
    }
}

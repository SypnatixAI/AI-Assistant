using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Queries;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class OrganizationMemberQueriesUpdateRoleTests
{
    [Fact]
    public async Task Given_TheSameRole_When_UpdateRole_Then_ReturnsWithoutTrackingOrChangingMember()
    {
        // Given
        var member = CreateMember(OrganizationRole.User);
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new AssistantCoreDbContext(options);
        var queries = new OrganizationMemberQueries(dbContext);

        // When
        var result = await queries.UpdateRole(
            member,
            OrganizationRole.User,
            CancellationToken.None);

        // Then
        Assert.Same(member, result);
        Assert.Equal(OrganizationRole.User, result.Role);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Given_AChangedRole_When_UpdateRole_Then_PersistsRoleWithoutChangingOtherFields()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var member = CreateMember(OrganizationRole.User);
        var originalName = member.Name;
        var originalEmail = member.Email;
        var originalStatus = member.Status;
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new AssistantCoreDbContext(options);
        dbContext.OrganizationMembers.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        var queries = new OrganizationMemberQueries(dbContext);

        // When
        var result = await queries.UpdateRole(
            member,
            OrganizationRole.Admin,
            cancellationToken);

        // Then
        dbContext.ChangeTracker.Clear();
        var persistedMember = await dbContext.OrganizationMembers.SingleAsync(
            candidate => candidate.Id == member.Id,
            cancellationToken);
        Assert.Same(member, result);
        Assert.Equal(OrganizationRole.Admin, persistedMember.Role);
        Assert.Equal(originalName, persistedMember.Name);
        Assert.Equal(originalEmail, persistedMember.Email);
        Assert.Equal(originalStatus, persistedMember.Status);
    }

    private static OrganizationMember CreateMember(OrganizationRole role) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Name = "Target Member",
        Email = "target@example.com",
        IdentityProvider = IdentityProvider.MicrosoftEntraId,
        ExternalUserId = Guid.NewGuid().ToString(),
        Role = role,
        Status = RecordStatus.Active
    };
}

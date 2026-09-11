using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Tests.Repository;

public sealed class Microsoft365DriveRepositoryTests
{
    [Fact]
    public async Task Given_AOneDrive_When_Saved_Then_ItDoesNotRequireASharePointSite()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var connection = CreateConnection(organizationId);
        await using var dbContext = CreateDbContext();
        var repository = new Microsoft365DriveRepository(dbContext);

        // When
        var drive = await repository.SaveOneDriveAsync(
            connection,
            "drive-alice",
            "owner-alice",
            "alice@contoso.com",
            "Alice OneDrive",
            "https://contoso-my.sharepoint.com/personal/alice",
            DateTimeOffset.UtcNow);

        // Then
        Assert.Null(drive.SiteId);
        Assert.Equal(Microsoft365SourceKind.OneDrive, drive.Kind);
        Assert.Equal("owner-alice", drive.OwnerUserObjectId);
        Assert.Equal("alice@contoso.com", drive.OwnerUserPrincipalName);
        Assert.Equal(organizationId, drive.OrganizationId);
    }

    [Fact]
    public async Task Given_TheSameDriveIdInTwoOrganizations_When_Queried_Then_OrganizationsRemainIsolated()
    {
        // Given
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var repository = new Microsoft365DriveRepository(dbContext);
        await repository.SaveOneDriveAsync(
            CreateConnection(firstOrganizationId),
            "same-drive",
            "owner-1",
            null,
            "First",
            null,
            DateTimeOffset.UtcNow);
        await repository.SaveOneDriveAsync(
            CreateConnection(secondOrganizationId),
            "same-drive",
            "owner-2",
            null,
            "Second",
            null,
            DateTimeOffset.UtcNow);

        // When
        var first = await repository.FindAsync(firstOrganizationId, "same-drive");
        var second = await repository.FindAsync(secondOrganizationId, "same-drive");

        // Then
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(firstOrganizationId, first.OrganizationId);
        Assert.Equal(secondOrganizationId, second.OrganizationId);
    }

    [Fact]
    public async Task Given_AnExistingOneDrive_When_SavedForAnotherOwner_Then_ItIsRejected()
    {
        // Given
        var organizationId = Guid.NewGuid();
        var connection = CreateConnection(organizationId);
        await using var dbContext = CreateDbContext();
        var repository = new Microsoft365DriveRepository(dbContext);
        await repository.SaveOneDriveAsync(
            connection,
            "drive-alice",
            "owner-alice",
            null,
            "Alice",
            null,
            DateTimeOffset.UtcNow);

        // When
        var action = () => repository.SaveOneDriveAsync(
            connection,
            "drive-alice",
            "owner-bob",
            null,
            "Bob",
            null,
            DateTimeOffset.UtcNow);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    private static Microsoft365Connection CreateConnection(Guid organizationId) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        OrganizationConnectorId = Guid.NewGuid()
    };

    private static AssistantCoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistantCoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AssistantCoreDbContext(options);
    }
}

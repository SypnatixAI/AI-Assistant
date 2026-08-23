using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365DocumentWorkFactoryTests
{
    [Theory, AutoDomainData]
    public void Given_TheSameFileVersionTwice_When_Create_Then_UsesSameProcessDeduplicationKey(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        string eTag,
        DateTimeOffset createdAt)
    {
        // Given
        var drive = CreateDrive(organizationId, sourceId);
        var item = CreateItem(itemId, "report.pdf", eTag, isDeleted: false);
        var factory = new Microsoft365DocumentWorkFactory();

        // When
        var firstWork = factory.Create(drive, item, createdAt);
        var replayedWork = factory.Create(drive, item, createdAt.AddMinutes(1));

        // Then
        Assert.Equal(Microsoft365DocumentWorkType.ProcessDocument, firstWork.WorkType);
        Assert.Equal(firstWork.DeduplicationKey, replayedWork.DeduplicationKey);
        Assert.Equal(64, firstWork.DeduplicationKey.Length);
        Assert.Equal("report.pdf", firstWork.Name);
        Assert.Equal(eTag, firstWork.ETag);
    }

    [Theory, AutoDomainData]
    public void Given_AChangedDocumentETag_When_Create_Then_UsesANewProcessDeduplicationKey(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        string firstETag,
        string secondETag,
        DateTimeOffset createdAt)
    {
        // Given
        var drive = CreateDrive(organizationId, sourceId);
        var factory = new Microsoft365DocumentWorkFactory();

        // When
        var firstWork = factory.Create(
            drive,
            CreateItem(itemId, "report.pdf", firstETag, isDeleted: false),
            createdAt);
        var modifiedWork = factory.Create(
            drive,
            CreateItem(itemId, "report.pdf", secondETag, isDeleted: false),
            createdAt.AddMinutes(1));

        // Then
        Assert.NotEqual(firstWork.DeduplicationKey, modifiedWork.DeduplicationKey);
    }

    [Theory, AutoDomainData]
    public void Given_ADeletedItem_When_Create_Then_CreatesDeleteWorkWithoutDocumentPayload(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        DateTimeOffset createdAt)
    {
        // Given
        var drive = CreateDrive(organizationId, sourceId);
        var item = CreateItem(itemId, null, null, isDeleted: true);
        var factory = new Microsoft365DocumentWorkFactory();

        // When
        var work = factory.Create(drive, item, createdAt);

        // Then
        Assert.Equal(Microsoft365DocumentWorkType.DeleteDocument, work.WorkType);
        Assert.Equal(itemId, work.DriveItemId);
        Assert.Null(work.Name);
        Assert.Null(work.ETag);
        Assert.Null(work.MimeType);
    }

    [Theory, AutoDomainData]
    public void Given_TheSameDeletedDocumentTwice_When_Create_Then_UsesSameDeleteDeduplicationKey(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        DateTimeOffset createdAt)
    {
        // Given
        var drive = CreateDrive(organizationId, sourceId);
        var item = CreateItem(itemId, null, null, isDeleted: true);
        var factory = new Microsoft365DocumentWorkFactory();

        // When
        var firstWork = factory.Create(drive, item, createdAt);
        var replayedWork = factory.Create(drive, item, createdAt.AddMinutes(1));

        // Then
        Assert.Equal(firstWork.DeduplicationKey, replayedWork.DeduplicationKey);
    }

    private static Microsoft365Drive CreateDrive(Guid organizationId, Guid sourceId) =>
        new()
        {
            Id = sourceId,
            OrganizationId = organizationId,
            SiteId = "site-id",
            DriveId = "drive-id"
        };

    private static Microsoft365DriveItemDelta CreateItem(
        string itemId,
        string? name,
        string? eTag,
        bool isDeleted) =>
        new(
            itemId,
            name,
            eTag,
            DateTimeOffset.Parse("2026-08-01T10:00:00Z"),
            DateTimeOffset.Parse("2026-08-02T11:00:00Z"),
            "https://contoso/document",
            42,
            "application/pdf",
            isDeleted,
            IsFolder: false,
            IsFile: !isDeleted);
}

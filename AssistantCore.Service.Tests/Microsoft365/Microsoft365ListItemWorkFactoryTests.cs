using System.Text.Json;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ListItemWorkFactoryTests
{
    [Theory, AutoDomainData]
    public void Given_TheSameActiveItemVersionTwice_When_Create_Then_UsesSameProcessDeduplicationKey(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        string eTag,
        DateTimeOffset createdAt)
    {
        // Given
        var list = CreateList(organizationId, sourceId);
        var item = CreateItem(itemId, eTag, isDeleted: false);
        var factory = new Microsoft365ListItemWorkFactory();

        // When
        var firstWork = factory.Create(list, item, createdAt);
        var replayedWork = factory.Create(list, item, createdAt.AddMinutes(1));

        // Then
        Assert.Equal(Microsoft365ListItemWorkType.ProcessListItem, firstWork.WorkType);
        Assert.Equal(firstWork.DeduplicationKey, replayedWork.DeduplicationKey);
        Assert.Equal(64, firstWork.DeduplicationKey.Length);
        Assert.Equal(eTag, firstWork.ETag);
        Assert.Contains("Title", firstWork.FieldsJson, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public void Given_AChangedETag_When_Create_Then_UsesANewProcessDeduplicationKey(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        string firstETag,
        string secondETag,
        DateTimeOffset createdAt)
    {
        // Given
        var list = CreateList(organizationId, sourceId);
        var factory = new Microsoft365ListItemWorkFactory();

        // When
        var firstWork = factory.Create(
            list,
            CreateItem(itemId, firstETag, isDeleted: false),
            createdAt);
        var modifiedWork = factory.Create(
            list,
            CreateItem(itemId, secondETag, isDeleted: false),
            createdAt.AddMinutes(1));

        // Then
        Assert.NotEqual(firstWork.DeduplicationKey, modifiedWork.DeduplicationKey);
    }

    [Theory, AutoDomainData]
    public void Given_ADeletedItem_When_Create_Then_CreatesDeleteWorkWithoutContent(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        DateTimeOffset createdAt)
    {
        // Given
        var list = CreateList(organizationId, sourceId);
        var item = CreateItem(itemId, eTag: null, isDeleted: true);
        var factory = new Microsoft365ListItemWorkFactory();

        // When
        var work = factory.Create(list, item, createdAt);

        // Then
        Assert.Equal(Microsoft365ListItemWorkType.DeleteListItem, work.WorkType);
        Assert.Equal(itemId, work.ListItemId);
        Assert.Null(work.ETag);
        Assert.Null(work.FieldsJson);
    }

    [Theory, AutoDomainData]
    public void Given_TheSameDeletedItemTwice_When_Create_Then_UsesSameDeleteDeduplicationKey(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        DateTimeOffset createdAt)
    {
        // Given
        var list = CreateList(organizationId, sourceId);
        var item = CreateItem(itemId, eTag: null, isDeleted: true);
        var factory = new Microsoft365ListItemWorkFactory();

        // When
        var firstWork = factory.Create(list, item, createdAt);
        var replayedWork = factory.Create(list, item, createdAt.AddMinutes(1));

        // Then
        Assert.Equal(firstWork.DeduplicationKey, replayedWork.DeduplicationKey);
    }

    private static Microsoft365List CreateList(Guid organizationId, Guid sourceId) =>
        new()
        {
            Id = sourceId,
            OrganizationId = organizationId,
            SiteId = "site-id",
            ListId = "list-id"
        };

    private static Microsoft365ListItemDelta CreateItem(
        string itemId,
        string? eTag,
        bool isDeleted)
    {
        JsonElement? fields = null;
        if (!isDeleted)
        {
            using var document = JsonDocument.Parse("{\"Title\":\"Request\"}");
            fields = document.RootElement.Clone();
        }

        return new Microsoft365ListItemDelta(
            itemId,
            eTag,
            DateTimeOffset.Parse("2026-08-01T10:00:00Z"),
            DateTimeOffset.Parse("2026-08-02T11:00:00Z"),
            "https://contoso/items/1",
            fields,
            isDeleted);
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365ListItemWorkFactory : IMicrosoft365ListItemWorkFactory
{
    public Microsoft365ListItemWorkData Create(
        Microsoft365List list,
        Microsoft365ListItemDelta item,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(item);

        var workType = item.IsDeleted
            ? Microsoft365ListItemWorkType.DeleteListItem
            : Microsoft365ListItemWorkType.ProcessListItem;
        var version = item.IsDeleted
            ? "delete"
            : !string.IsNullOrWhiteSpace(item.ETag)
                ? item.ETag
                : throw new InvalidOperationException(
                    "An active Microsoft 365 list item requires an eTag.");
        var fieldsJson = item.IsDeleted
            ? null
            : item.Fields is { ValueKind: JsonValueKind.Object } fields
                ? fields.GetRawText()
                : throw new InvalidOperationException(
                    "An active Microsoft 365 list item requires fields.");

        return new Microsoft365ListItemWorkData(
            list.OrganizationId,
            workType,
            list.SiteId,
            list.ListId,
            item.Id,
            item.IsDeleted ? null : item.ETag,
            item.CreatedDateTime,
            item.LastModifiedDateTime,
            item.WebUrl,
            fieldsJson,
            CreateDeduplicationKey(list.OrganizationId, list.Id, item.Id, version),
            createdAt);
    }

    private static string CreateDeduplicationKey(
        Guid organizationId,
        Guid sourceId,
        string itemId,
        string version)
    {
        var identity = JsonSerializer.Serialize(new[]
        {
            organizationId.ToString("N"),
            sourceId.ToString("N"),
            itemId,
            version
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365DocumentWorkFactory : IMicrosoft365DocumentWorkFactory
{
    public Microsoft365DocumentWorkData Create(
        Microsoft365Drive drive,
        Microsoft365DriveItemDelta item,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(drive);
        ArgumentNullException.ThrowIfNull(item);

        if (!item.IsDeleted && (!item.IsFile || string.IsNullOrWhiteSpace(item.Name)))
        {
            throw new InvalidOperationException(
                "Only a deleted item or an active Microsoft 365 file can create document work.");
        }

        var workType = item.IsDeleted
            ? Microsoft365DocumentWorkType.DeleteDocument
            : Microsoft365DocumentWorkType.ProcessDocument;
        var version = item.IsDeleted
            ? "delete"
            : !string.IsNullOrWhiteSpace(item.ETag)
                ? item.ETag
                : throw new InvalidOperationException(
                    "An active Microsoft 365 file requires an eTag.");

        return new Microsoft365DocumentWorkData(
            drive.OrganizationId,
            workType,
            drive.SiteId,
            drive.DriveId,
            item.Id,
            item.IsDeleted ? null : item.Name,
            item.IsDeleted ? null : item.ETag,
            item.CreatedDateTime,
            item.LastModifiedDateTime,
            item.WebUrl,
            item.IsDeleted ? null : item.Size,
            item.IsDeleted ? null : item.MimeType,
            CreateDeduplicationKey(drive.OrganizationId, drive.Id, item.Id, version),
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

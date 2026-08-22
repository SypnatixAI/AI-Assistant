using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ListResponse(
    string SiteId,
    string ListId,
    string DisplayName,
    string? WebUrl,
    string Status,
    bool IsIndexed)
{
    public static Microsoft365ListResponse FromList(Microsoft365List list) =>
        new(
            list.SiteId,
            list.ListId,
            list.DisplayName,
            list.WebUrl,
            list.Status.ToString(),
            list.IsIndexed);
}

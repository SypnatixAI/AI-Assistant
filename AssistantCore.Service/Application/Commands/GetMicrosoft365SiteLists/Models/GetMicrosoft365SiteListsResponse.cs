using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists.Models;

public sealed record GetMicrosoft365SiteListsResponse(
    IReadOnlyCollection<Microsoft365ListResponse> Lists);

using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365Sites.Models;

public sealed record GetMicrosoft365SitesResponse(
    IReadOnlyCollection<Microsoft365AvailableSiteResponse> Sites);

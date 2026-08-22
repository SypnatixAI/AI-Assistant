using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists.Models;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists;

public sealed class GetMicrosoft365SiteListsCommandHandler(
    IMicrosoft365ListConsultationService listConsultationService)
    : IRequestHandler<GetMicrosoft365SiteListsCommand, GetMicrosoft365SiteListsResponse>
{
    public async Task<GetMicrosoft365SiteListsResponse> HandleAsync(
        GetMicrosoft365SiteListsCommand request,
        CancellationToken cancellationToken)
    {
        var lists = await listConsultationService.GetListsAsync(
            request.SiteId,
            cancellationToken);

        return new GetMicrosoft365SiteListsResponse(
            lists.Select(Microsoft365ListResponse.FromList).ToArray());
    }
}

using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists.Models;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists;

public sealed record GetMicrosoft365SiteListsCommand(string SiteId)
    : IRequest<GetMicrosoft365SiteListsResponse>;

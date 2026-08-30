using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMicrosoft365Sites.Models;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365Sites;

public sealed record GetMicrosoft365SitesCommand : IRequest<GetMicrosoft365SitesResponse>;

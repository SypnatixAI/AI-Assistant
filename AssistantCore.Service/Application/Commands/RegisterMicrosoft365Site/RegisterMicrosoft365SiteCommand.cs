using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.RegisterMicrosoft365Site;

public sealed record RegisterMicrosoft365SiteCommand(string SiteId) : IRequest<Microsoft365SiteResponse>;

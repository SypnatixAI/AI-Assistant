using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.EnableMicrosoft365List;

public sealed record EnableMicrosoft365ListCommand(
    string SiteId,
    string ListId,
    bool IsIndexed) : IRequest<Microsoft365ListResponse>;

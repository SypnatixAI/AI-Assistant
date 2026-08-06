using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMembers.Models;

namespace AssistantCore.Service.Application.Commands.GetMembers;

public sealed record GetMembersCommand() : IRequest<GetMembersResponse>;

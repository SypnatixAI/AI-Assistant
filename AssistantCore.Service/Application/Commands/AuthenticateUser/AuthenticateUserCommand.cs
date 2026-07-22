using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.AuthenticateUser.Models;

namespace AssistantCore.Service.Application.Commands.AuthenticateUser;

public sealed record AuthenticateUserCommand() : IRequest<AuthenticateUserResponse>;

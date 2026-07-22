namespace AssistantCore.Service.Application.Commands.AuthenticateUser.Models;

public sealed record CurrentUserResponse(
    Guid Id,
    string DisplayName,
    string Email);
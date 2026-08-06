namespace AssistantCore.Service.Application.Commands.AuthenticateUser.Models;

public sealed record AuthenticateUserResponse(
    CurrentUserResponse User,
    CurrentOrganizationResponse Organization,
    IReadOnlyCollection<string> Roles);

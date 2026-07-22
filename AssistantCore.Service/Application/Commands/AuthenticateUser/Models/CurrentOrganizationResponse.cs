namespace AssistantCore.Service.Application.Commands.AuthenticateUser.Models;

public sealed record CurrentOrganizationResponse(
    Guid Id,
    string Name);
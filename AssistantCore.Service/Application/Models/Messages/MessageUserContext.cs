using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Models.Messages;

public sealed record MessageUserContext(
    Organization Organization,
    OrganizationMember Member);

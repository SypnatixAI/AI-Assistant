using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Messages;

namespace AssistantCore.Service.Application.Services.Messages.Authorization;

public sealed class MessageUserContextService(
    ICurrentIdentity currentIdentity,
    IOrganizationQueries organizationQueries,
    IOrganizationMemberQueries memberQueries) : IMessageUserContextService
{
    public async Task<MessageUserContext> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        var identity = currentIdentity.GetIdentity();
        var organization = await organizationQueries.FindOrganization(
            identity.Provider,
            identity.ExternalOrganizationId,
            cancellationToken);

        if (organization is null || organization.Status != RecordStatus.Active)
        {
            throw new ForbiddenException("Organization access denied.");
        }

        var member = await memberQueries.FindMember(
            organization.Id,
            identity.Provider,
            identity.ExternalUserId,
            cancellationToken);

        if (member is null
            || member.Status != RecordStatus.Active
            || member.Role is not (OrganizationRole.Admin or OrganizationRole.User))
        {
            throw new ForbiddenException("Organization member access denied.");
        }

        return new MessageUserContext(organization, member);
    }
}

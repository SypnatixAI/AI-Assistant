using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public interface IOrganizationRoleResolver
{
    /// <summary>
    /// Derive le role interne a partir des app roles Entra du token.
    /// Leve <see cref="AssistantCore.Repository.Abstractions.ForbiddenException"/>
    /// lorsque le role d'admission requis est absent, meme si tenantAdmin est present.
    /// </summary>
    OrganizationRole Resolve(IReadOnlyCollection<string> appRoles);
}

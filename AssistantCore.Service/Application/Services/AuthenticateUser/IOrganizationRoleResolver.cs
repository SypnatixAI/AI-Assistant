using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public interface IOrganizationRoleResolver
{
    /// <summary>
    /// Dérive le rôle effectif de la session à partir des app roles Entra du token.
    /// La valeur retournée sert aux autorisations courantes et ne doit pas être
    /// synchronisée dans le rôle informatif conservé en base.
    /// Leve <see cref="AssistantCore.Repository.Abstractions.ForbiddenException"/>
    /// lorsque le role d'admission requis est absent, meme si tenantAdmin est present.
    /// </summary>
    OrganizationRole Resolve(IReadOnlyCollection<string> appRoles);
}

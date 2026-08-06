using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public interface IAuthenticateUserService
{
    Task<(Organization Organization, OrganizationMember Member)> GetOrganizationAsync(CancellationToken cancellationToken);
}

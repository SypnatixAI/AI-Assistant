using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Application.Models.Messages;

namespace AssistantCore.Service.Tests.Messages;

public sealed class MessageUserContextTests
{
    [Theory, AutoDomainData]
    public void Given_CurrentIdentityDiffersFromStoredMember_When_CreateConnectorExecutionContext_Then_UsesCurrentIdentity(
        Organization organization,
        OrganizationMember member,
        AuthenticatedIdentity identity)
    {
        // Given
        var currentUserId = Guid.NewGuid();
        var storedUserId = Guid.NewGuid();
        identity = identity with
        {
            Provider = IdentityProvider.MicrosoftEntraId,
            ExternalUserId = currentUserId.ToString("D"),
            Email = "current.user@contoso.com",
            ExternalOrganizationId = "current-tenant-id"
        };
        organization.ExternalTenantId = "stored-tenant-id";
        member.IdentityProvider = IdentityProvider.MicrosoftEntraId;
        member.ExternalUserId = storedUserId.ToString("D");
        member.Email = string.Empty;
        var context = new MessageUserContext(organization, member, identity);

        // When
        var result = context.CreateConnectorExecutionContext();

        // Then
        Assert.Equal(identity.ExternalOrganizationId, result.ExternalTenantId);
        Assert.Equal(currentUserId, result.EntraUserId);
        Assert.Equal(identity.Provider, result.IdentityProvider);
        Assert.Equal(identity.Email, result.UserEmail);
    }
}

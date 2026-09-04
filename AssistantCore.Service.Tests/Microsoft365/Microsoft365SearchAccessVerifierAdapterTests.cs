using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Service.Application.Services.Microsoft365;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SearchAccessVerifierAdapterTests
{
    [Theory, AutoDomainData]
    public async Task Given_FreshAclGrantsCurrentGroup_When_KeepAuthorizedAsync_Then_KeepsRecord(
        Guid organizationId,
        Guid userId,
        Guid allowedGroupId,
        string tenantId,
        string title,
        string content)
    {
        // Given
        var record = CreateRecord(title, content);
        var acl = new Microsoft365Acl(
            [],
            [allowedGroupId.ToString("D")],
            [],
            false,
            false,
            Microsoft365AclInheritance.Unique);
        var verifier = new Microsoft365SearchAccessVerifierAdapter(
            new AclResolverFake(new Microsoft365AclResolution.ResolvedAcl(acl)));

        // When
        var results = await verifier.KeepAuthorizedAsync(
            organizationId,
            tenantId,
            userId.ToString("D"),
            [allowedGroupId.ToString("D")],
            [record],
            CancellationToken.None);

        // Then
        Assert.Equal([record], results);
    }

    [Theory, AutoDomainData]
    public async Task Given_FreshAclDoesNotGrantCurrentPrincipal_When_KeepAuthorizedAsync_Then_RemovesRecord(
        Guid organizationId,
        Guid userId,
        Guid allowedUserId,
        string tenantId,
        string title,
        string content)
    {
        // Given
        var acl = new Microsoft365Acl(
            [allowedUserId.ToString("D")],
            [],
            [],
            false,
            false,
            Microsoft365AclInheritance.Unique);
        var verifier = new Microsoft365SearchAccessVerifierAdapter(
            new AclResolverFake(new Microsoft365AclResolution.ResolvedAcl(acl)));

        // When
        var results = await verifier.KeepAuthorizedAsync(
            organizationId,
            tenantId,
            userId.ToString("D"),
            [],
            [CreateRecord(title, content)],
            CancellationToken.None);

        // Then
        Assert.Empty(results);
    }

    [Theory]
    [InlineAutoDomainData(true, false)]
    [InlineAutoDomainData(false, true)]
    public async Task Given_FreshAclOnlyGrantsLinkAccess_When_KeepAuthorizedAsync_Then_RemovesRecord(
        bool hasAnonymousLink,
        bool hasOrganizationLink,
        Guid organizationId,
        Guid userId,
        string tenantId,
        string title,
        string content)
    {
        // Given
        var acl = new Microsoft365Acl(
            [],
            [],
            [],
            hasAnonymousLink,
            hasOrganizationLink,
            Microsoft365AclInheritance.Unique);
        var verifier = new Microsoft365SearchAccessVerifierAdapter(
            new AclResolverFake(new Microsoft365AclResolution.ResolvedAcl(acl)));

        // When
        var results = await verifier.KeepAuthorizedAsync(
            organizationId,
            tenantId,
            userId.ToString("D"),
            [],
            [CreateRecord(title, content)],
            CancellationToken.None);

        // Then
        Assert.Empty(results);
    }

    [Theory, AutoDomainData]
    public async Task Given_AclCannotBeResolved_When_KeepAuthorizedAsync_Then_RemovesRecord(
        Guid organizationId,
        Guid userId,
        string tenantId,
        string title,
        string content)
    {
        // Given
        var verifier = new Microsoft365SearchAccessVerifierAdapter(
            new AclResolverFake(new Microsoft365AclResolution.Unresolved(
                Microsoft365AclResolutionFailureReason.PartialResponse)));

        // When
        var results = await verifier.KeepAuthorizedAsync(
            organizationId,
            tenantId,
            userId.ToString("D"),
            [],
            [CreateRecord(title, content)],
            CancellationToken.None);

        // Then
        Assert.Empty(results);
    }

    private static Microsoft365SearchRecord CreateRecord(string title, string content) => new(
        "Microsoft365",
        title,
        content,
        "chunk-id",
        "site-id",
        "drive-id",
        "drive-item-id",
        null,
        null,
        1d);

    private sealed class AclResolverFake(Microsoft365AclResolution result)
        : IMicrosoft365AclResolver
    {
        public Task<Microsoft365AclResolution> ResolveAsync(
            Organization organization,
            Microsoft365ContentReference contentReference,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}

using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Service.Application.Services.Microsoft365;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SecurityReadCachingTests
{
    [Theory, AutoDomainData]
    public async Task Given_RepeatedGroupResolution_When_CacheIsWarm_Then_DoesNotCallGraphAgain(
        Guid userId,
        Guid memberGroupId,
        Guid ownedGroupId)
    {
        // Given
        var tokenRequestCount = 0;
        var graphRequestCount = 0;
        using var tokenHttpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            tokenRequestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"access-token\",\"expires_in\":3600}")
            };
        }));
        using var graphHttpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            graphRequestCount++;
            var groupId = request.RequestUri!.AbsolutePath.Contains(
                "/ownedObjects/",
                StringComparison.Ordinal)
                ? ownedGroupId
                : memberGroupId;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"value\":[{{\"id\":\"{groupId:D}\"}}]}}")
            };
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new Microsoft365UserGroupResolverAdapter(
            new MicrosoftIdentityClient(tokenHttpClient),
            new MicrosoftGraphUserGroupClient(graphHttpClient),
            new Microsoft365SecurityIdentityNormalizer(),
            Options.Create(new Microsoft365Options
            {
                AuthorityBaseUrl = "https://login.microsoftonline.com",
                GraphBaseUrl = "https://graph.microsoft.com",
                ClientId = Guid.NewGuid().ToString("D"),
                ClientSecret = "client-secret"
            }),
            cache);

        // When
        var first = await resolver.ResolveGroupIdsAsync(
            "tenant-id",
            userId.ToString("D"),
            CancellationToken.None);
        var second = await resolver.ResolveGroupIdsAsync(
            "tenant-id",
            userId.ToString("D"),
            CancellationToken.None);

        // Then
        Assert.Equal(first, second);
        Assert.Contains(memberGroupId.ToString("D"), second);
        Assert.Equal(2, second.Count);
        Assert.Equal(1, tokenRequestCount);
        Assert.Equal(2, graphRequestCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_RepeatedAclVerification_When_CacheIsWarm_Then_ResolvesDocumentAclOnce(
        Guid organizationId,
        Guid userId,
        string tenantId)
    {
        // Given
        var acl = new Microsoft365Acl(
            [userId.ToString("D")],
            [],
            [],
            false,
            false,
            Microsoft365AclInheritance.Unique);
        var aclResolver = new CountingAclResolver(
            new Microsoft365AclResolution.ResolvedAcl(acl));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var verifier = new Microsoft365SearchAccessVerifierAdapter(
            aclResolver,
            memoryCache: cache);
        var record = new Microsoft365SearchRecord(
            "Microsoft365",
            "Workbook",
            "content",
            "chunk-id",
            "site-id",
            "drive-id",
            "drive-item-id",
            null,
            null,
            1d);

        // When
        var first = await verifier.KeepAuthorizedAsync(
            organizationId,
            tenantId,
            userId.ToString("D"),
            [],
            [],
            [record],
            CancellationToken.None);
        var second = await verifier.KeepAuthorizedAsync(
            organizationId,
            tenantId,
            userId.ToString("D"),
            [],
            [],
            [record],
            CancellationToken.None);

        // Then
        Assert.Equal([record], first);
        Assert.Equal([record], second);
        Assert.Equal(1, aclResolver.ResolveCount);
    }

    private sealed class CountingAclResolver(Microsoft365AclResolution result)
        : IMicrosoft365AclResolver
    {
        public int ResolveCount { get; private set; }

        public Task<Microsoft365AclResolution> ResolveAsync(
            Organization organization,
            Microsoft365ContentReference contentReference,
            CancellationToken cancellationToken = default)
        {
            ResolveCount++;
            return Task.FromResult(result);
        }
    }
}

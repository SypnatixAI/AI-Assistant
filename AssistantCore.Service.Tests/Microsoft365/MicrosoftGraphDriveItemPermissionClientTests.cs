using System.Net;
using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphDriveItemPermissionClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_PagedDriveItemPermissions_When_GetPermissionsAsync_Then_ReturnsEveryPermission(
        string accessToken)
    {
        // Given
        var requestUris = new List<Uri>();
        var authorizations = new List<AuthenticationHeaderValue?>();
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUris.Add(request.RequestUri!);
            authorizations.Add(request.Headers.Authorization);
            requestCount++;
            return CreateResponse(requestCount == 1
                ? "{\"value\":[{\"id\":\"permission-1\",\"roles\":[\"read\",\"write\"],\"grantedToV2\":{\"user\":{\"id\":\"user-1\",\"displayName\":\"Ada\"},\"siteUser\":{\"id\":\"7\",\"displayName\":\"Ada\",\"loginName\":\"i:0#.f|membership|ada@contoso.com\"}},\"inheritedFrom\":{\"driveId\":\"drive-1\",\"id\":\"folder-1\",\"path\":\"/drive/root:/Shared\"}}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/drives/drive/items/item/permissions?$skiptoken=next\"}"
                : "{\"value\":[{\"id\":\"permission-2\",\"roles\":[\"read\"],\"grantedToIdentitiesV2\":[{\"group\":{\"id\":\"group-1\",\"displayName\":\"Finance\"},\"siteGroup\":{\"id\":\"12\",\"displayName\":\"Visitors\"},\"sharePointGroup\":{\"id\":\"sp-group-1\",\"displayName\":\"Members\"}}],\"link\":{\"type\":\"view\",\"scope\":\"anonymous\",\"webUrl\":\"https://contoso.sharepoint.com/:b:/r/report.pdf\",\"preventsDownload\":true,\"application\":{\"id\":\"app-1\",\"displayName\":\"App\"}}}]}");
        }));
        var client = new MicrosoftGraphDriveItemPermissionClient(httpClient);

        // When
        var permissions = await client.GetPermissionsAsync(
            "https://graph.microsoft.com",
            accessToken,
            "drive/id",
            "item/id",
            CancellationToken.None);

        // Then
        Assert.Collection(
            permissions,
            permission =>
            {
                Assert.Equal("permission-1", permission.Id);
                Assert.Equal(["read", "write"], permission.Roles);
                Assert.Equal("user-1", permission.GrantedToV2?.User?.Id);
                Assert.Equal("7", permission.GrantedToV2?.SiteUser?.Id);
                Assert.Equal("i:0#.f|membership|ada@contoso.com", permission.GrantedToV2?.SiteUser?.LoginName);
                Assert.Equal("folder-1", permission.InheritedFrom?.Id);
                Assert.Equal("/drive/root:/Shared", permission.InheritedFrom?.Path);
            },
            permission =>
            {
                Assert.Equal("permission-2", permission.Id);
                var identitySet = Assert.Single(permission.GrantedToIdentitiesV2);
                Assert.Equal("group-1", identitySet.Group?.Id);
                Assert.Equal("12", identitySet.SiteGroup?.Id);
                Assert.Equal("sp-group-1", identitySet.SharePointGroup?.Id);
                Assert.Equal("anonymous", permission.Link?.Scope);
                Assert.Equal("view", permission.Link?.Type);
                Assert.True(permission.Link?.PreventsDownload);
                Assert.Equal("app-1", permission.Link?.Application?.Id);
            });
        Assert.Equal(2, requestCount);
        Assert.Equal("/v1.0/drives/drive%2Fid/items/item%2Fid/permissions", requestUris[0].AbsolutePath);
        Assert.All(authorizations, authorization =>
        {
            Assert.Equal("Bearer", authorization?.Scheme);
            Assert.Equal(accessToken, authorization?.Parameter);
        });
    }

    [Theory, InlineAutoDomainData("https://untrusted.example/v1.0/drives/drive/items/item/permissions")]
    public async Task Given_AnUntrustedNextLink_When_GetPermissionsAsync_Then_RejectsPaginationUrl(
        string nextLink,
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            CreateResponse($"{{\"value\":[],\"@odata.nextLink\":\"{nextLink}\"}}")));
        var client = new MicrosoftGraphDriveItemPermissionClient(httpClient);

        // When
        var action = () => client.GetPermissionsAsync(
            "https://graph.microsoft.com",
            accessToken,
            "drive-id",
            "item-id",
            CancellationToken.None);

        // Then
        var exception = await Assert.ThrowsAsync<MicrosoftExternalException>(action);
        Assert.Contains("not trusted", exception.Message, StringComparison.Ordinal);
    }

    private static HttpResponseMessage CreateResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };
}

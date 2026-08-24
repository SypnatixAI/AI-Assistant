using System.Net;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftSharePointListItemPermissionClientTests
{
    [Theory, InlineAutoDomainData(42)]
    public async Task Given_AListItemWithUniquePermissions_When_GetPermissionsAsync_Then_ReturnsItemRoleAssignments(
        int itemId,
        Guid listId,
        string accessToken)
    {
        // Given
        var responses = new SharePointResponseSequence(
            """{"HasUniqueRoleAssignments":true}""",
            RoleAssignmentsJson);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(responses.Respond));
        var client = new MicrosoftSharePointListItemPermissionClient(httpClient);

        // When
        var result = await client.GetPermissionsAsync(
            SiteUrl,
            accessToken,
            listId,
            itemId,
            CancellationToken.None);

        // Then
        var resolved = Assert.IsType<MicrosoftSharePointListItemPermissionReadResult.Resolved>(result);
        Assert.Equal(MicrosoftSharePointPermissionInheritanceSource.ListItem, resolved.InheritanceSource);
        var permission = Assert.Single(resolved.Permissions);
        Assert.Equal(17, permission.Principal.Id);
        Assert.Equal("4cf950a2-47bd-46db-9c3f-50f8fceec8a0", permission.Principal.EntraObjectId);
        Assert.Equal(8, permission.Principal.PrincipalType);
        Assert.Equal("Contribute", Assert.Single(permission.RoleDefinitions).Name);
        Assert.Equal(2, responses.Requests.Count);
        Assert.All(responses.Requests, request => Assert.Equal($"Bearer {accessToken}", request.Authorization));
        Assert.All(responses.Requests, request => Assert.Contains("odata=nometadata", request.Accept));
        Assert.Contains("AadObjectId", responses.Requests[1].Uri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("graph.microsoft.com", responses.Requests[0].Uri.Host, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, InlineAutoDomainData(42)]
    public async Task Given_AListItemInheritingFromAList_When_GetPermissionsAsync_Then_ReturnsListRoleAssignments(
        int itemId,
        Guid listId,
        string accessToken)
    {
        // Given
        var responses = new SharePointResponseSequence(
            """{"HasUniqueRoleAssignments":false}""",
            """{"HasUniqueRoleAssignments":true}""",
            RoleAssignmentsJson);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(responses.Respond));
        var client = new MicrosoftSharePointListItemPermissionClient(httpClient);

        // When
        var result = await client.GetPermissionsAsync(
            SiteUrl,
            accessToken,
            listId,
            itemId,
            CancellationToken.None);

        // Then
        var resolved = Assert.IsType<MicrosoftSharePointListItemPermissionReadResult.Resolved>(result);
        Assert.Equal(MicrosoftSharePointPermissionInheritanceSource.List, resolved.InheritanceSource);
        Assert.Contains("/RoleAssignments", responses.Requests[2].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain($"items({itemId})", responses.Requests[2].Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Theory, InlineAutoDomainData(42)]
    public async Task Given_AListItemInheritingFromTheSite_When_GetPermissionsAsync_Then_ReturnsSiteRoleAssignments(
        int itemId,
        Guid listId,
        string accessToken)
    {
        // Given
        var responses = new SharePointResponseSequence(
            """{"HasUniqueRoleAssignments":false}""",
            """{"HasUniqueRoleAssignments":false}""",
            RoleAssignmentsJson);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(responses.Respond));
        var client = new MicrosoftSharePointListItemPermissionClient(httpClient);

        // When
        var result = await client.GetPermissionsAsync(
            SiteUrl,
            accessToken,
            listId,
            itemId,
            CancellationToken.None);

        // Then
        var resolved = Assert.IsType<MicrosoftSharePointListItemPermissionReadResult.Resolved>(result);
        Assert.Equal(MicrosoftSharePointPermissionInheritanceSource.Site, resolved.InheritanceSource);
        Assert.EndsWith("/_api/web/RoleAssignments", responses.Requests[2].Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Theory, InlineAutoDomainData(42)]
    public async Task Given_APartialRoleAssignmentResponse_When_GetPermissionsAsync_Then_ReturnsPartialResponseUnresolved(
        int itemId,
        Guid listId,
        string accessToken)
    {
        // Given
        var responses = new SharePointResponseSequence(
            """{"HasUniqueRoleAssignments":true}""",
            """{"value":[{"Member":{"Id":17,"Title":"Visitors","PrincipalType":8}}]}""");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(responses.Respond));
        var client = new MicrosoftSharePointListItemPermissionClient(httpClient);

        // When
        var result = await client.GetPermissionsAsync(
            SiteUrl,
            accessToken,
            listId,
            itemId,
            CancellationToken.None);

        // Then
        var unresolved = Assert.IsType<MicrosoftSharePointListItemPermissionReadResult.Unresolved>(result);
        Assert.Equal(MicrosoftSharePointPermissionUnresolvedReason.PartialResponse, unresolved.Reason);
    }

    [Theory, InlineAutoDomainData(42)]
    public async Task Given_AnUnknownPrincipal_When_GetPermissionsAsync_Then_ReturnsUnknownPrincipalUnresolved(
        int itemId,
        Guid listId,
        string accessToken)
    {
        // Given
        var responses = new SharePointResponseSequence(
            """{"HasUniqueRoleAssignments":true}""",
            """
            {"value":[{
              "Member":{"Id":0,"Title":"Unknown","PrincipalType":0},
              "RoleDefinitionBindings":[{"Id":1073741827,"Name":"Contribute","RoleTypeKind":3}]
            }]}
            """);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(responses.Respond));
        var client = new MicrosoftSharePointListItemPermissionClient(httpClient);

        // When
        var result = await client.GetPermissionsAsync(
            SiteUrl,
            accessToken,
            listId,
            itemId,
            CancellationToken.None);

        // Then
        var unresolved = Assert.IsType<MicrosoftSharePointListItemPermissionReadResult.Unresolved>(result);
        Assert.Equal(MicrosoftSharePointPermissionUnresolvedReason.UnknownPrincipal, unresolved.Reason);
    }

    private const string SiteUrl = "https://contoso.sharepoint.com/sites/engineering";

    private const string RoleAssignmentsJson = """
        {"value":[{
          "Member":{"Id":17,"Title":"Engineering Visitors","LoginName":"Engineering Visitors","PrincipalType":8,"AadObjectId":{"NameId":"4cf950a2-47bd-46db-9c3f-50f8fceec8a0"}},
          "RoleDefinitionBindings":[{"Id":1073741827,"Name":"Contribute","RoleTypeKind":3}]
        }]}
        """;

    private sealed class SharePointResponseSequence(params string[] payloads)
    {
        private readonly Queue<string> remainingPayloads = new(payloads);

        public List<CapturedRequest> Requests { get; } = [];

        public HttpResponseMessage Respond(HttpRequestMessage request)
        {
            Requests.Add(new CapturedRequest(
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                string.Join(",", request.Headers.GetValues("Accept"))));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(remainingPayloads.Dequeue())
            };
        }
    }

    private sealed record CapturedRequest(Uri Uri, string? Authorization, string Accept);
}

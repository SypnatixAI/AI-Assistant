using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphUserGroupClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_GroupsOwnedByUser_When_GetOwnedGroupIdsAsync_Then_ReturnsCanonicalIdentifiers(
        Guid userId,
        Guid firstGroupId,
        Guid secondGroupId,
        string accessToken)
    {
        // Given
        Uri? capturedUri = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$$"""
                    {"value":[{"id":"{{{firstGroupId:D}}}"},{"id":"{{{secondGroupId:D}}}"}]}
                    """)
            };
        }));
        var client = new MicrosoftGraphUserGroupClient(httpClient);

        // When
        var result = await client.GetOwnedGroupIdsAsync(
            "https://graph.microsoft.com",
            accessToken,
            userId.ToString("D"),
            CancellationToken.None);

        // Then
        Assert.Equal([firstGroupId.ToString("D"), secondGroupId.ToString("D")], result);
        Assert.Equal(
            $"/v1.0/users/{userId:D}/ownedObjects/microsoft.graph.group",
            capturedUri!.AbsolutePath);
    }
}

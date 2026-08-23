using System.Net;
using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ListSchemaClientAdapterTests
{
    [Theory, AutoDomainData]
    public async Task Given_AList_When_GetColumnsAsync_Then_AcquiresTenantTokenAndMapsGraphColumns(
        string tenantId,
        string accessToken,
        string columnId)
    {
        // Given
        Uri? tokenRequestUri = null;
        AuthenticationHeaderValue? graphAuthorization = null;
        using var identityHttpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            tokenRequestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"access_token\":\"{accessToken}\",\"expires_in\":3600}}")
            };
        }));
        using var graphHttpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            graphAuthorization = request.Headers.Authorization;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"value\":[{{\"id\":\"{columnId}\",\"name\":\"Title\",\"text\":{{}}}}]}}")
            };
        }));
        var adapter = new Microsoft365ListSchemaClientAdapter(
            new MicrosoftIdentityClient(identityHttpClient),
            new MicrosoftGraphListSchemaClient(graphHttpClient),
            Options.Create(new Microsoft365Options
            {
                AuthorityBaseUrl = "https://login.microsoftonline.com",
                GraphBaseUrl = "https://graph.microsoft.com",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            }));

        // When
        var columns = await adapter.GetColumnsAsync(
            tenantId,
            "site-id",
            "list-id",
            CancellationToken.None);

        // Then
        Assert.Contains(Uri.EscapeDataString(tenantId), tokenRequestUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(accessToken, graphAuthorization?.Parameter);
        var column = Assert.Single(columns);
        Assert.Equal(columnId, column.Id);
        Assert.Equal("Title", column.Definition.GetProperty("name").GetString());
    }
}

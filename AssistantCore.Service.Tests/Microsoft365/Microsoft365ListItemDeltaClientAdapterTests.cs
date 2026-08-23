using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ListItemDeltaClientAdapterTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnInitialDelta_When_GetInitialPagesAsync_Then_AcquiresTenantTokenAndMapsPage(
        string tenantId,
        string accessToken,
        string itemId,
        string eTag)
    {
        // Given
        Uri? tokenRequestUri = null;
        string? graphBearerToken = null;
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
            graphBearerToken = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"value\":[{{\"id\":\"{itemId}\",\"eTag\":\"{eTag}\",\"fields\":{{\"Title\":\"Request\"}}}}],\"@odata.deltaLink\":\"https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=opaque\"}}")
            };
        }));
        var adapter = new Microsoft365ListItemDeltaClientAdapter(
            new MicrosoftIdentityClient(identityHttpClient),
            new MicrosoftGraphListItemDeltaClient(graphHttpClient),
            Options.Create(new Microsoft365Options
            {
                AuthorityBaseUrl = "https://login.microsoftonline.com",
                GraphBaseUrl = "https://graph.microsoft.com",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            }));

        // When
        var pages = new List<AssistantCore.Service.Application.Models.Microsoft365.Microsoft365ListItemDeltaPage>();
        await foreach (var page in adapter.GetInitialPagesAsync(
                           tenantId,
                           "site-id",
                           "list-id",
                           CancellationToken.None))
        {
            pages.Add(page);
        }

        // Then
        Assert.Contains(Uri.EscapeDataString(tenantId), tokenRequestUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(accessToken, graphBearerToken);
        var item = Assert.Single(Assert.Single(pages).Items);
        Assert.Equal(itemId, item.Id);
        Assert.Equal(eTag, item.ETag);
    }
}

using System.Net;
using System.Text.Json;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ConsentClientAdapterTests
{
    [Theory, AutoDomainData]
    public async Task Given_AdminConsent_When_CompleteAdminConsentAsync_Then_ValidatesTenantWithApplicationToken(
        Guid tenantId,
        string accessToken)
    {
        // Given
        var tenant = tenantId.ToString("D");
        using var httpClient = CreateHttpClient(tenant, accessToken);
        var adapter = CreateAdapter(httpClient);

        // When
        var result = await adapter.CompleteAdminConsentAsync(tenant, CancellationToken.None);

        // Then
        Assert.Equal(tenant, result.TenantId);
        Assert.Equal(accessToken, result.AccessToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnexpectedGraphTenant_When_CompleteAdminConsentAsync_Then_RequestIsRejected(
        Guid callbackTenantId,
        Guid graphTenantId,
        string accessToken)
    {
        // Given
        using var httpClient = CreateHttpClient(graphTenantId.ToString("D"), accessToken);
        var adapter = CreateAdapter(httpClient);

        // When
        var action = () => adapter.CompleteAdminConsentAsync(
            callbackTenantId.ToString("D"),
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<Microsoft365ExternalException>(action);
    }

    [Theory, AutoDomainData]
    public async Task Given_GraphPermissionsArePropagating_When_CompleteAdminConsentAsync_Then_RetriesTenantValidation(
        Guid tenantId,
        string accessToken)
    {
        // Given
        var tenant = tenantId.ToString("D");
        var graphRequestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "login.microsoftonline.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        access_token = accessToken,
                        expires_in = 3600
                    }))
                };
            }

            graphRequestCount++;
            if (graphRequestCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    value = new[] { new { id = tenant, displayName = "Tenant" } }
                }))
            };
        }));
        var adapter = CreateAdapter(httpClient);

        // When
        var result = await adapter.CompleteAdminConsentAsync(tenant, CancellationToken.None);

        // Then
        Assert.Equal(tenant, result.TenantId);
        Assert.Equal(2, graphRequestCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_GraphPermissionsRemainUnavailable_When_CompleteAdminConsentAsync_Then_ReturnsTheConsentFailure(
        Guid tenantId,
        string accessToken)
    {
        // Given
        var tenant = tenantId.ToString("D");
        var graphRequestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "login.microsoftonline.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        access_token = accessToken,
                        expires_in = 3600
                    }))
                };
            }

            graphRequestCount++;
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }));
        var adapter = CreateAdapter(httpClient);

        // When
        var action = () => adapter.CompleteAdminConsentAsync(tenant, CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<Microsoft365ExternalException>(action);
        Assert.Equal(3, graphRequestCount);
    }

    private static HttpClient CreateHttpClient(string graphTenantId, string accessToken) =>
        new(new StubHttpMessageHandler(request =>
            request.RequestUri?.Host == "login.microsoftonline.com"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        access_token = accessToken,
                        expires_in = 3600
                    }))
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        value = new[]
                        {
                            new { id = graphTenantId, displayName = "Tenant" }
                        }
                    }))
                }));

    private static Microsoft365ConsentClientAdapter CreateAdapter(HttpClient httpClient) =>
        new(
            new MicrosoftIdentityClient(httpClient),
            new MicrosoftGraphClient(httpClient),
            Options.Create(new Microsoft365Options
            {
                AuthorityBaseUrl = "https://login.microsoftonline.com",
                GraphBaseUrl = "https://graph.microsoft.com",
                ClientId = "client-id",
                ClientSecret = "client-secret",
                ConsentCallbackUrl = "https://localhost/callback"
            }),
            TimeProvider.System);
}

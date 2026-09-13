using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SubscriptionClientAdapterTests
{
    [Theory, AutoDomainData]
    public async Task Given_GraphRenewalReturnsNotFound_When_RenewAsync_Then_ApplicationReceivesMissingResult(
        string tenantId,
        string subscriptionId,
        string notificationUrl,
        string accessToken,
        DateTimeOffset expiresAt)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            request.RequestUri?.Host == "login.microsoftonline.com"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"access_token":"{{accessToken}}","expires_in":3600}""")
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound)));
        var adapter = CreateAdapter(httpClient);

        // When
        var result = await adapter.RenewAsync(
            tenantId,
            subscriptionId,
            notificationUrl,
            expiresAt,
            CancellationToken.None);

        // Then
        Assert.False(result.Exists);
        Assert.Null(result.Subscription);
    }

    [Theory, AutoDomainData]
    public async Task Given_GraphRejectsNotificationValidation_When_RenewAsync_Then_ApplicationReceivesMissingResult(
        string tenantId,
        string subscriptionId,
        string notificationUrl,
        string accessToken,
        DateTimeOffset expiresAt)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            request.RequestUri?.Host == "login.microsoftonline.com"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"access_token":"{{accessToken}}","expires_in":3600}""")
                }
                : new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        """{"error":{"code":"ValidationError","message":"Subscription validation request failed. HTTP status code is 'NotFound'."}}""")
                }));
        var adapter = CreateAdapter(httpClient);

        // When
        var result = await adapter.RenewAsync(
            tenantId,
            subscriptionId,
            notificationUrl,
            expiresAt,
            CancellationToken.None);

        // Then
        Assert.False(result.Exists);
        Assert.Null(result.Subscription);
    }

    private static Microsoft365SubscriptionClientAdapter CreateAdapter(HttpClient httpClient) =>
        new(
            new MicrosoftIdentityClient(httpClient),
            new MicrosoftGraphSubscriptionClient(httpClient),
            Options.Create(new Microsoft365Options
            {
                AuthorityBaseUrl = "https://login.microsoftonline.com",
                GraphBaseUrl = "https://graph.microsoft.com",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            }));
}

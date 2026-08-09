using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssistantCore.Service.Tests.Integration;

public sealed class ApplicationStartupTests
{
    [Fact]
    public async Task Given_ValidDevelopmentConfiguration_When_StartingApplication_Then_RootEndpointRespondsWithoutError()
    {
        // Given
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseEnvironment(Environments.Development));

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        // When
        using var response = await client.GetAsync("/");

        // Then
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location?.OriginalString);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.AuthenticateUser.Models;
using AssistantCore.Service.Infrastructure.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace AssistantCore.Service.Tests.Integration;

public sealed class LocalJwtAuthenticationTests
{
    private const string Issuer = "AssistantCore.Local";
    private const string Audience = "AssistantCore.Api";
    private const string SigningKey = "assistant-core-local-signing-key-not-a-secret-2026";

    [Theory, AutoDomainData]
    public async Task Given_AValidLocalJwt_When_AuthenticateUser_Then_AuthorizationIsGranted(
        Guid userId,
        Guid organizationId)
    {
        // Given
        var expectedResponse = new AuthenticateUserResponse(
            new CurrentUserResponse(userId, "Administrateur local", "admin@local.test"),
            new CurrentOrganizationResponse(organizationId, "Organisation locale"),
            ["Admin"]);
        var dispatcher = new RecordingDispatcher { Response = expectedResponse };
        await using var factory = CreateLocalFactory(dispatcher);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(SigningKey, "access_as_user"));

        // When
        using var response = await client.GetAsync("/api/core/authenticateUser");

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dispatcher.ReceivedRequest);
    }

    [Theory, AutoDomainData]
    public async Task Given_ALocalJwtWithAnInvalidSignature_When_AuthenticateUser_Then_ReturnsUnauthorized(
        Guid userId,
        Guid organizationId)
    {
        // Given
        var dispatcher = new RecordingDispatcher
        {
            Response = new AuthenticateUserResponse(
                new CurrentUserResponse(userId, "Administrateur local", "admin@local.test"),
                new CurrentOrganizationResponse(organizationId, "Organisation locale"),
                ["Admin"])
        };
        await using var factory = CreateLocalFactory(dispatcher);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("another-local-signing-key-with-at-least-32-bytes", "access_as_user"));

        // When
        using var response = await client.GetAsync("/api/core/authenticateUser");

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(dispatcher.ReceivedRequest);
    }

    [Theory, InlineAutoDomainData("Production")]
    public void Given_LocalJwtOutsideTheLocalEnvironment_When_AddApiAuthentication_Then_Throws(
        string environmentName)
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Authentication:Mode"] = "LocalJwt",
                    ["Authentication:LocalJwt:Issuer"] = Issuer,
                    ["Authentication:LocalJwt:Audience"] = Audience,
                    ["Authentication:LocalJwt:SigningKey"] = SigningKey
                })
            .Build();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = environmentName
        };

        // When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddApiAuthentication(configuration, environment));

        // Then
        Assert.Contains("only allowed", exception.ToString(), StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateLocalFactory(RecordingDispatcher dispatcher) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Local");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDispatcher>();
                    services.AddSingleton<IDispatcher>(dispatcher);
                });
            });

    private static string CreateToken(string signingKey, string scope)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim("tid", "00000000-0000-0000-0000-000000000100"),
                new Claim("oid", "00000000-0000-0000-0000-000000000200"),
                new Claim("name", "Administrateur local"),
                new Claim("preferred_username", "admin@local.test"),
                new Claim("scp", scope)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = nameof(LocalJwtAuthenticationTests);

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

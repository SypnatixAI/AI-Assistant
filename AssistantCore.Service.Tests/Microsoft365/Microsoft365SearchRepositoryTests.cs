using System.Net;
using System.Text.Json;
using AssistantCore.ExternalServices.Services.Azure;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SearchRepositoryTests
{
    [Theory, AutoDomainData]
    public async Task Given_AuthenticatedSecurityContext_When_SearchAsync_Then_ImposesOrganizationUserAndGroups(
        Guid organizationId,
        Guid userId,
        Guid firstGroupId,
        Guid secondGroupId,
        string apiKey,
        string query,
        string chunkId,
        string title,
        string content)
    {
        // Given
        string? payload = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            payload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    value = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["@search.score"] = 1.0,
                            ["chunkId"] = chunkId,
                            ["title"] = title,
                            ["content"] = content
                        }
                    }
                }))
            };
        }));
        var repository = new Microsoft365SearchRepository(
            new AzureAiSearchPassageSearchClient(httpClient),
            Options.Create(new AzureAiSearchOptions
            {
                Endpoint = "https://search.example",
                IndexName = "content-index",
                ApiKey = apiKey
            }));
        var parameters = new Microsoft365SearchParameters(
            query,
            ["sharepoint"],
            null,
            null,
            new Microsoft365SearchSecurityContext(
                organizationId,
                userId.ToString("D"),
                [firstGroupId.ToString("D"), secondGroupId.ToString("D")]),
            10);

        // When
        var results = await repository.SearchAsync(parameters, CancellationToken.None);

        // Then
        Assert.Single(results);
        using var document = JsonDocument.Parse(payload!);
        var filter = document.RootElement.GetProperty("filter").GetString();
        Assert.Contains($"organizationId eq '{organizationId:D}'", filter, StringComparison.Ordinal);
        Assert.Contains("isAvailable eq true", filter, StringComparison.Ordinal);
        Assert.Contains($"allowedUserIds/any(id: id eq '{userId:D}')", filter, StringComparison.Ordinal);
        Assert.Contains(firstGroupId.ToString("D"), filter, StringComparison.Ordinal);
        Assert.Contains(secondGroupId.ToString("D"), filter, StringComparison.Ordinal);
        Assert.Equal(
            "chunkId,title,content",
            document.RootElement.GetProperty("select").GetString());
        Assert.DoesNotContain("allowedUserIds", document.RootElement.GetProperty("select").GetString());
    }
}

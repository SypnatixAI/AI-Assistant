using System.Net;
using System.Text.Json;
using AssistantCore.ExternalServices.Services.OpenAI;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class OpenAiEmbeddingsClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_ValidInputs_When_CreateAsync_Then_ReturnsVectorsInProviderOrder(
        string apiKey,
        string firstInput,
        string secondInput)
    {
        // Given
        string? requestPayload = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestPayload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"data":[
                      {"index":1,"embedding":[0.3,0.4]},
                      {"index":0,"embedding":[0.1,0.2]}
                    ]}
                    """)
            };
        }));
        var client = new OpenAiEmbeddingsClient(httpClient);

        // When
        var vectors = await client.CreateAsync(
            "https://api.openai.com/v1",
            apiKey,
            "text-embedding-3-small",
            2,
            [firstInput, secondInput],
            CancellationToken.None);

        // Then
        Assert.Equal([0.1f, 0.2f], vectors[0]);
        Assert.Equal([0.3f, 0.4f], vectors[1]);
        using var payload = JsonDocument.Parse(requestPayload!);
        Assert.Equal(2, payload.RootElement.GetProperty("dimensions").GetInt32());
        Assert.Equal(2, payload.RootElement.GetProperty("input").GetArrayLength());
    }
}

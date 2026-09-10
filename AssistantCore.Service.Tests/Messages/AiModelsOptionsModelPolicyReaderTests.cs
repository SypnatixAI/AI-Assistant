using AssistantCore.Service.Infrastructure.AiModels;
using AssistantCore.Service.Infrastructure.AiModels.Configuration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AiModelsOptionsModelPolicyReaderTests
{
    [Fact]
    public async Task Given_MultipleProvidersAndModels_When_GetPolicyAsync_Then_ReturnsOnlyEnabledModelsWithoutProviderSecrets()
    {
        // Given
        var options = new AiModelsOptions
        {
            DefaultModel = "gpt-5.6-luna",
            Providers = new Dictionary<string, AiModelProviderOptions>
            {
                ["OpenAI"] = new()
                {
                    Enabled = true,
                    Endpoint = "https://api.openai.com/v1",
                    ApiKey = "super-secret-key",
                    TimeoutSeconds = 60,
                    Models = new Dictionary<string, AiModelOptions>
                    {
                        ["gpt-5.6-luna"] = new()
                        {
                            Enabled = true,
                            DisplayName = "Luna",
                            Description = "Modele general recommande pour la plupart des questions."
                        },
                        ["gpt-5.6-terra"] = new()
                        {
                            Enabled = false,
                            DisplayName = "Terra",
                            Description = "Modele adapte aux analyses plus detaillees."
                        }
                    }
                },
                ["Anthropic"] = new()
                {
                    Enabled = false,
                    Models = new Dictionary<string, AiModelOptions>
                    {
                        ["claude-x"] = new()
                        {
                            Enabled = true,
                            DisplayName = "Claude X",
                            Description = "Ne doit jamais apparaitre : le fournisseur est desactive."
                        }
                    }
                }
            }
        };
        var reader = new AiModelsOptionsModelPolicyReader(Options.Create(options));

        // When
        var policy = await reader.GetPolicyAsync(Guid.NewGuid(), CancellationToken.None);

        // Then
        Assert.Equal("gpt-5.6-luna", policy.DefaultModelId);
        var model = Assert.Single(policy.Models);
        Assert.Equal("gpt-5.6-luna", model.Id);
        Assert.Equal("Luna", model.DisplayName);
        Assert.Equal("Modele general recommande pour la plupart des questions.", model.Description);
        Assert.DoesNotContain(
            policy.Models,
            entry => entry.Id == "gpt-5.6-terra" || entry.Id == "claude-x");
    }
}

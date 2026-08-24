using AssistantCore.Repository.Persistence;
using AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Infrastructure.Connectors;
using AssistantCore.Service.Infrastructure.Connectors.InternalData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Tests.Integration;

public sealed class ConnectorServiceCollectionExtensionsTests
{
    [Theory]
    [InlineAutoDomainData(20, 4000)]
    public void Given_ValidConnectorConfiguration_When_AddConnectorInfrastructure_Then_RegistersInternalDataModule(
        int maximumResults,
        int maximumContentLength)
    {
        // Given
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Connectors:InternalData:EnabledCategories:0"] = "Conversations",
                ["Connectors:InternalData:EnabledCategories:1"] = "Messages",
                ["Connectors:InternalData:MaximumResults"] = maximumResults.ToString(),
                ["Connectors:InternalData:MaximumContentLength"] = maximumContentLength.ToString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddDbContext<AssistantCoreDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // When
        services.AddConnectorInfrastructure(configuration);

        // Then
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<InternalDataConnectorOptions>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEvidenceNormalizer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IInternalDataSearchRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IInternalDataConnector>());
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IAiToolExecutionHandler)
                && descriptor.ImplementationType == typeof(InternalDataToolExecutionHandler));
    }
}

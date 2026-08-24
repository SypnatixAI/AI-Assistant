using AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Infrastructure.Connectors.InternalData;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Infrastructure.Connectors;

public static class ConnectorServiceCollectionExtensions
{
    private const string InternalDataSectionName = "Connectors:InternalData";
    private const string Microsoft365SectionName = "Connectors:Microsoft365";
    private const int MaximumAllowedResults = 100;

    public static IServiceCollection AddConnectorInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = CreateInternalDataOptions(configuration);
        var microsoft365Options = CreateMicrosoft365Options(configuration);

        services.AddSingleton(options);
        services.AddSingleton(microsoft365Options);
        services.AddSingleton<IEvidenceNormalizer, EvidenceNormalizer>();
        services.AddScoped<IInternalDataSearchRepository, InternalDataSearchRepository>();
        services.AddScoped<IInternalDataConnector, InternalDataConnector>();
        services.AddScoped<IAiToolExecutionHandler, InternalDataToolExecutionHandler>();
        services.AddScoped<IMicrosoft365SearchRepository, Microsoft365SearchRepositoryAdapter>();
        services.AddScoped<IMicrosoft365Connector, Microsoft365Connector>();
        services.AddScoped<IAiToolExecutionHandler, Microsoft365SearchToolExecutionHandler>();

        return services;
    }

    private static Microsoft365ConnectorOptions CreateMicrosoft365Options(
        IConfiguration configuration)
    {
        var section = configuration.GetSection(Microsoft365SectionName);
        var maximumResults = section.GetValue<int?>(
            nameof(Microsoft365ConnectorOptions.MaximumResults)) ?? 10;
        var maximumContentLength = section.GetValue<int?>(
            nameof(Microsoft365ConnectorOptions.MaximumContentLength)) ?? 4000;

        if (maximumResults is <= 0 or > MaximumAllowedResults)
        {
            throw new InvalidOperationException(
                $"Invalid configuration '{Microsoft365SectionName}': "
                + $"{nameof(Microsoft365ConnectorOptions.MaximumResults)} must be between 1 and {MaximumAllowedResults}.");
        }

        if (maximumContentLength <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid configuration '{Microsoft365SectionName}': "
                + $"{nameof(Microsoft365ConnectorOptions.MaximumContentLength)} must be greater than zero.");
        }

        return new Microsoft365ConnectorOptions(maximumResults, maximumContentLength);
    }

    private static InternalDataConnectorOptions CreateInternalDataOptions(
        IConfiguration configuration)
    {
        var section = configuration.GetSection(InternalDataSectionName);
        var enabledCategories = section
            .GetSection(nameof(InternalDataConnectorOptions.EnabledCategories))
            .GetChildren()
            .Select(item => ParseCategory(item.Value))
            .ToHashSet();
        var maximumResults = section.GetValue<int>(
            nameof(InternalDataConnectorOptions.MaximumResults));
        var maximumContentLength = section.GetValue<int>(
            nameof(InternalDataConnectorOptions.MaximumContentLength));

        if (enabledCategories.Count == 0)
        {
            throw CreateConfigurationException(
                $"{nameof(InternalDataConnectorOptions.EnabledCategories)} must contain at least one value.");
        }

        if (maximumResults is <= 0 or > MaximumAllowedResults)
        {
            throw CreateConfigurationException(
                $"{nameof(InternalDataConnectorOptions.MaximumResults)} must be between 1 and {MaximumAllowedResults}.");
        }

        if (maximumContentLength <= 0)
        {
            throw CreateConfigurationException(
                $"{nameof(InternalDataConnectorOptions.MaximumContentLength)} must be greater than zero.");
        }

        return new InternalDataConnectorOptions(
            enabledCategories,
            maximumResults,
            maximumContentLength);
    }

    private static InternalDataCategory ParseCategory(string? value)
    {
        if (Enum.TryParse<InternalDataCategory>(value, ignoreCase: true, out var category)
            && Enum.IsDefined(category))
        {
            return category;
        }

        throw CreateConfigurationException(
            $"'{value}' is not a supported internal data category.");
    }

    private static InvalidOperationException CreateConfigurationException(string message) =>
        new($"Invalid configuration '{InternalDataSectionName}': {message}");
}

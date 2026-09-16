using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Tabular;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Infrastructure.Connectors;

public static class ConnectorServiceCollectionExtensions
{
    public static IServiceCollection AddConnectorInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;

        services.AddScoped<IToolExecutionRouter, ScopedToolExecutionRouter>();
        services.AddScoped<IMicrosoft365SpreadsheetDocumentResolver, Microsoft365SpreadsheetDocumentResolver>();
        services.AddScoped<IAiToolExecutionHandler, Microsoft365SpreadsheetAnalysisToolExecutionHandler>();

        return services;
    }
}

using AssistantCore.Repository.Persistence;
using AssistantCore.Service.Application;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssistantCore.Ingestion.Worker;

public static class WorkerProgram
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        if (string.IsNullOrWhiteSpace(
                builder.Configuration.GetConnectionString("AssistantCoreDatabase")))
        {
            throw new InvalidOperationException(
                "Connection string 'AssistantCoreDatabase' is required by the ingestion worker.");
        }

        builder.Services.AddOptions<Microsoft365WorkerOptions>()
            .Bind(builder.Configuration.GetSection(Microsoft365WorkerOptions.SectionName))
            .Validate(
                options => !options.RunStartupConnectionCheck || options.StartupConnectionId is not null,
                $"{Microsoft365WorkerOptions.SectionName}:StartupConnectionId is required when the startup connection check is enabled.")
            .ValidateOnStart();

        builder.Services.AddMicrosoft365Application();
        builder.Services.AddMicrosoft365Infrastructure(builder.Configuration);
        builder.Services.AddPersistence(builder.Configuration);
        builder.Services.AddHostedService<Microsoft365IngestionWorker>();

        await builder.Build().RunAsync();
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using AssistantCore.ExternalServices.Services.Azure;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365SynchronizationPublisherAdapter : IMicrosoft365SynchronizationPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AzureServiceBusPublisherClient publisherClient;
    private readonly ServiceBusOptions options;

    public Microsoft365SynchronizationPublisherAdapter(
        AzureServiceBusPublisherClient publisherClient,
        IOptions<ServiceBusOptions> options)
    {
        this.publisherClient = publisherClient;
        this.options = options.Value;
    }

    public Task PublishAsync(
        Microsoft365SynchronizationWork work,
        CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(
            new SynchronizationMessage(
                work.WorkType,
                work.SubscriptionId,
                work.SiteId,
                work.ListId,
                work.DriveId),
            SerializerOptions);
        var queueName = work.WorkType switch
        {
            "SynchronizeDrive" => options.DriveSyncQueue,
            "SynchronizeList" => options.ListSyncQueue,
            _ => throw new InvalidOperationException($"Unsupported Microsoft 365 work type '{work.WorkType}'.")
        };
        return publisherClient.PublishAsync(
            queueName,
            body,
            work.WorkId.ToString("D"),
            work.WorkType,
            cancellationToken);
    }

    private sealed record SynchronizationMessage(
        string WorkType,
        string SubscriptionId,
        string? SiteId,
        string? ListId,
        string? DriveId);
}

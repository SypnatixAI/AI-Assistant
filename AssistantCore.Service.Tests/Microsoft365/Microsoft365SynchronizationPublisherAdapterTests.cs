using System.Text.Json;
using AssistantCore.ExternalServices.Services.Azure;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SynchronizationPublisherAdapterTests
{
    [Theory, AutoDomainData]
    public async Task Given_AListSynchronization_When_PublishAsync_Then_MessageContainsOnlyTechnicalIdentifiers(
        Guid workId,
        string subscriptionId,
        string siteId,
        string listId)
    {
        // Given
        await using var client = new RecordingServiceBusPublisherClient();
        var adapter = new Microsoft365SynchronizationPublisherAdapter(
            client,
            Options.Create(new ServiceBusOptions
            {
                FullyQualifiedNamespace = "test.servicebus.windows.net",
                ListSyncQueue = "list-sync",
                DriveSyncQueue = "drive-sync"
            }));

        // When
        await adapter.PublishAsync(
            new Microsoft365SynchronizationWork(
                workId,
                "SynchronizeList",
                subscriptionId,
                siteId,
                listId,
                DriveId: null),
            CancellationToken.None);

        // Then
        Assert.Equal("list-sync", client.QueueName);
        Assert.Equal(workId.ToString("D"), client.MessageId);
        using var json = JsonDocument.Parse(client.Body!);
        Assert.Equal(4, json.RootElement.EnumerateObject().Count());
        Assert.Equal("SynchronizeList", json.RootElement.GetProperty("workType").GetString());
        Assert.Equal(subscriptionId, json.RootElement.GetProperty("subscriptionId").GetString());
        Assert.Equal(siteId, json.RootElement.GetProperty("siteId").GetString());
        Assert.Equal(listId, json.RootElement.GetProperty("listId").GetString());
        Assert.DoesNotContain("organization", client.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientState", client.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content", client.Body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingServiceBusPublisherClient()
        : AzureServiceBusPublisherClient("test.servicebus.windows.net")
    {
        public string? QueueName { get; private set; }

        public string? Body { get; private set; }

        public string? MessageId { get; private set; }

        public override Task PublishAsync(
            string queueName,
            string body,
            string messageId,
            string subject,
            CancellationToken cancellationToken = default)
        {
            QueueName = queueName;
            Body = body;
            MessageId = messageId;
            return Task.CompletedTask;
        }
    }
}

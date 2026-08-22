using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace AssistantCore.ExternalServices.Services.Azure;

public class AzureServiceBusPublisherClient : IAsyncDisposable
{
    private readonly ServiceBusClient client;
    private readonly Dictionary<string, ServiceBusSender> senders = new(StringComparer.Ordinal);
    private readonly object senderLock = new();

    public AzureServiceBusPublisherClient(string fullyQualifiedNamespace)
    {
        client = new ServiceBusClient(fullyQualifiedNamespace, new DefaultAzureCredential());
    }

    public virtual Task PublishAsync(
        string queueName,
        string body,
        string messageId,
        string subject,
        CancellationToken cancellationToken = default)
    {
        var message = new ServiceBusMessage(body)
        {
            MessageId = messageId,
            Subject = subject,
            ContentType = "application/json"
        };
        return GetSender(queueName).SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in senders.Values)
        {
            await sender.DisposeAsync();
        }

        await client.DisposeAsync();
    }

    private ServiceBusSender GetSender(string queueName)
    {
        lock (senderLock)
        {
            if (!senders.TryGetValue(queueName, out var sender))
            {
                sender = client.CreateSender(queueName);
                senders.Add(queueName, sender);
            }

            return sender;
        }
    }
}

using System.Threading.Channels;
using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Usage;

public sealed class UsageConsumptionQueue
{
    private readonly Channel<TokenConsumption> channel = Channel.CreateUnbounded<TokenConsumption>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(TokenConsumption consumption)
    {
        if (!channel.Writer.TryWrite(consumption))
        {
            throw new InvalidOperationException("Unable to queue token consumption.");
        }
    }

    public IAsyncEnumerable<TokenConsumption> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);
}

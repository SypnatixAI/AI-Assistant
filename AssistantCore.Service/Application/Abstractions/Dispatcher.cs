using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Application.Abstractions;

public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    public async Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);

        var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync))
            ?? throw new InvalidOperationException($"No handler method found for request type '{requestType.Name}'.");

        var task = (Task<TResponse>)handleMethod.Invoke(handler, [request, cancellationToken])!;
        return await task.ConfigureAwait(false);
    }
}

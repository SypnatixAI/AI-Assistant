using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Application.Abstractions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDispatcher(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var handlerInterfaceType = typeof(IRequestHandler<,>);

        var handlerRegistrations = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Select(type => new
            {
                Implementation = type,
                Services = type.GetInterfaces()
                    .Where(@interface => @interface.IsGenericType
                        && @interface.GetGenericTypeDefinition() == handlerInterfaceType)
            })
            .Where(registration => registration.Services.Any());

        foreach (var registration in handlerRegistrations)
        {
            foreach (var serviceType in registration.Services)
            {
                services.AddScoped(serviceType, registration.Implementation);
            }
        }

        return services;
    }
}

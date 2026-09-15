using AssistantCore.Repository.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Infrastructure.Persistence;

public static class PersistenceEncryptionServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceEncryption(this IServiceCollection services)
    {
        services.AddSingleton<IFieldEncryptorFactory, DataProtectionFieldEncryptorFactory>();

        return services;
    }
}

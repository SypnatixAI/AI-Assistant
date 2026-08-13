using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AssistantCore.Repository.Queries;
using AssistantCore.Repository.Repositories;

namespace AssistantCore.Repository.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AssistantCoreDatabase")
            ?? throw new InvalidOperationException("Connection string 'AssistantCoreDatabase' is missing.");

        services.AddDbContext<AssistantCoreDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        services.AddScoped<IOrganizationMemberQueries, OrganizationMemberQueries>();
        services.AddScoped<IConversationRepository, ConversationRepository>();

        return services;
    }
}

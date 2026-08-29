namespace AssistantCore.Service.Infrastructure.Cors;

public static class ApiCorsServiceCollectionExtensions
{
    public const string PolicyName = "AssistantCoreSpa";

    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ApiCorsOptions.SectionName)
            .Get<ApiCorsOptions>() ?? new ApiCorsOptions();

        services.AddCors(corsOptions =>
            corsOptions.AddPolicy(
                PolicyName,
                policy => policy
                    .WithOrigins(options.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()));

        return services;
    }
}

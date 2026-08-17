using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Members;
using AssistantCore.Service.Application.Services.Messages;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticateUserService, AuthenticateUserService>();
        services.AddScoped<IMemberManagementService, MemberManagementService>();
        services.AddScoped<ISendMessageService, SendMessageService>();
        services.AddScoped<IAiModelTurnService, AiModelTurnService>();
        services.AddScoped<IToolCallBatchExecutor, ToolCallBatchExecutor>();
        services.AddScoped<IOrchestrationContinuationPolicy, OrchestrationContinuationPolicy>();
        services.AddScoped<IOrchestrationResultBuilder, OrchestrationResultBuilder>();
        services.AddSingleton<IEvidenceCitationResolver, EvidenceCitationResolver>();
        services.AddSingleton<IToolCallFingerprintGenerator, ToolCallFingerprintGenerator>();
        services.AddSingleton<IAiToolFailureWarningFactory, AiToolFailureWarningFactory>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IAiToolRegistry, AiToolRegistry>();
        services.AddScoped<IAiToolArgumentSchemaValidator, AiToolArgumentSchemaValidator>();
        services.AddScoped<IAiToolArgumentSecurityValidator, AiToolArgumentSecurityValidator>();
        services.AddScoped<IAiToolDateRangeValidator, AiToolDateRangeValidator>();
        services.AddScoped<IAiToolCallValidator, AiToolCallValidator>();
        services.AddScoped<IToolExecutionRouter, ToolExecutionRouter>();

        return services;
    }
}

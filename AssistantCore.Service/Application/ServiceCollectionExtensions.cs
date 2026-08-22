using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Members;
using AssistantCore.Service.Application.Services.Messages;
using AssistantCore.Service.Application.Services.Messages.Authorization;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Lifecycle;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Responses;
using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Validation;
using AssistantCore.Service.Application.Services.Microsoft365;
using AssistantCore.Service.Application.Services.Organizations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MessagesOptions>()
            .Bind(configuration.GetSection(MessagesOptions.SectionName))
            .Validate(
                options => options.MaximumMessageLength > 0,
                $"{MessagesOptions.SectionName}:{nameof(MessagesOptions.MaximumMessageLength)} must be greater than zero.")
            .ValidateOnStart();
        services.AddOptions<MessageOrchestrationOptions>()
            .Bind(configuration.GetSection(MessageOrchestrationOptions.SectionName))
            .Validate(
                options => options.MaximumExecutionTimeSeconds > 0
                    && options.MaximumToolCalls > 0
                    && options.MaximumModelTokens > 0
                    && options.MaximumEstimatedCost > 0
                    && options.MaximumResultsPerTool > 0
                    && options.MaximumContextSize > 0
                    && options.MaximumRepeatedToolCalls > 0
                    && options.MaximumParallelToolCalls > 0,
                $"Every value in {MessageOrchestrationOptions.SectionName} must be greater than zero.")
            .ValidateOnStart();
        services.AddScoped<ISendMessageCommandValidator, SendMessageCommandValidator>();
        services.AddScoped<IMessageUserContextService, MessageUserContextService>();
        services.AddScoped<IAuthenticateUserService, AuthenticateUserService>();
        services.AddScoped<IMemberManagementService, MemberManagementService>();
        services.AddScoped<IOrganizationManagementService, OrganizationManagementService>();
        services.AddMicrosoft365Application();
        services.AddScoped<IMessageProcessingLifecycleService, MessageProcessingLifecycleService>();
        services.AddScoped<IMessageToolOrchestrator, MessageToolOrchestrator>();
        services.AddSingleton<ISendMessageResponseFactory, SendMessageResponseFactory>();
        services.AddScoped<IAiModelTurnService, AiModelTurnService>();
        services.AddScoped<IToolCallBatchExecutor, ToolCallBatchExecutor>();
        services.AddScoped<IOrchestrationContinuationPolicy, OrchestrationContinuationPolicy>();
        services.AddScoped<IOrchestrationResultBuilder, OrchestrationResultBuilder>();
        services.AddSingleton<IEvidenceCitationResolver, EvidenceCitationResolver>();
        services.AddSingleton<IToolCallFingerprintGenerator, ToolCallFingerprintGenerator>();
        services.AddSingleton<IAiToolFailureWarningFactory, AiToolFailureWarningFactory>();
        services.AddScoped<IAiToolRegistry, AiToolRegistry>();
        services.AddScoped<IAiToolArgumentSchemaValidator, AiToolArgumentSchemaValidator>();
        services.AddScoped<IAiToolArgumentSecurityValidator, AiToolArgumentSecurityValidator>();
        services.AddScoped<IAiToolDateRangeValidator, AiToolDateRangeValidator>();
        services.AddScoped<IAiToolCallValidator, AiToolCallValidator>();
        services.AddScoped<IToolExecutionRouter, ToolExecutionRouter>();

        return services;
    }

    public static IServiceCollection AddMicrosoft365Application(this IServiceCollection services)
    {
        services.AddScoped<IMicrosoft365ConnectionService, Microsoft365ConnectionService>();
        services.AddScoped<IMicrosoft365IngestionOrchestrator, Microsoft365IngestionOrchestrator>();
        services.AddScoped<IMicrosoft365ListActivationService, Microsoft365ListActivationService>();
        services.AddScoped<IMicrosoft365ListConsultationService, Microsoft365ListConsultationService>();
        services.AddScoped<IMicrosoft365SiteSourcesDiscoveryService, Microsoft365SiteSourcesDiscoveryService>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        return services;
    }
}

using AssistantCore.Service.Application;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Infrastructure.Authentication;
using AssistantCore.Service.Infrastructure.AiModels;
using AssistantCore.Service.Infrastructure.Connectors;
using AssistantCore.Service.Infrastructure.Cors;
using AssistantCore.Service.Infrastructure.Microsoft365;
using AssistantCore.Service.Infrastructure.Health;
using AssistantCore.Service.Middleware;
using AssistantCore.Repository.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsEnvironment("Certif")
    || builder.Environment.IsEnvironment("LocalLive"))
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Add services to the container.

builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddApiCors(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Microsoft Entra ou JWT généré par scripts/start-local-wiremock.sh."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddAuthenticationInfrastructure(builder.Configuration);
builder.Services.AddAiModelInfrastructure(builder.Configuration);
builder.Services.AddConnectorInfrastructure(builder.Configuration);
builder.Services.AddMicrosoft365Infrastructure(builder.Configuration);
builder.Services.AddDispatcher(Assembly.GetExecutingAssembly());
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<SqlDatabaseHealthCheck>(SqlDatabaseHealthCheck.Name, tags: ["ready"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()
    || app.Environment.IsEnvironment("Local")
    || app.Environment.IsEnvironment("LocalLive")
    || app.Environment.IsEnvironment("Dev"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<Microsoft365ConsentCallbackRedirectMiddleware>();

app.UseHttpsRedirection();

app.UseCors(ApiCorsServiceCollectionExtensions.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.MapControllers();

app.Run();

public partial class Program;

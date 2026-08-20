using AssistantCore.Service.Application;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Infrastructure.Authentication;
using AssistantCore.Service.Infrastructure.AiModels;
using AssistantCore.Service.Infrastructure.Connectors;
using AssistantCore.Service.Infrastructure.Microsoft365;
using AssistantCore.Service.Middleware;
using AssistantCore.Repository.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
});
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddAuthenticationInfrastructure(builder.Configuration);
builder.Services.AddAiModelInfrastructure(builder.Configuration);
builder.Services.AddConnectorInfrastructure(builder.Configuration);
builder.Services.AddMicrosoft365Infrastructure(builder.Configuration);
builder.Services.AddDispatcher(Assembly.GetExecutingAssembly());
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();

app.Run();

public partial class Program;
using AssistantCore.Service.Application;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ApplicationRegistrationTests
{
    [Theory, AutoDomainData]
    public void Given_AWorker_When_AddMicrosoft365WorkerApplication_Then_RegistersOnlyBackgroundServices(
        bool _)
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddMicrosoft365WorkerApplication();

        // Then
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IMicrosoft365IngestionOrchestrator));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IMicrosoft365DocumentProcessingService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IMicrosoft365ConnectionService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IMicrosoft365ListActivationService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IMicrosoft365ListConsultationService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IMicrosoft365SiteSourcesDiscoveryService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IMicrosoft365DriveAdministrationService));
    }
}

using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Messages.Authorization;
using AssistantCore.Service.Application.Services.Models;

namespace AssistantCore.Service.Tests.Messages;

public sealed class ModelCatalogServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnActivePolicy_When_GetAvailableModelsAsync_Then_MapsTheDefaultModelAndPreservesOrder(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var policy = new ModelPolicy(
            "gpt-5.6-luna",
            [
                new ModelPolicyEntry("gpt-5.6-luna", "Luna", "Modele general."),
                new ModelPolicyEntry("gpt-5.6-terra", "Terra", "Modele detaille.")
            ]);
        var policyReader = new StubModelPolicyReader(policy);
        var service = new ModelCatalogService(
            new StubUserContextService(new MessageUserContext(organization, member)),
            policyReader);

        // When
        var response = await service.GetAvailableModelsAsync(CancellationToken.None);

        // Then
        Assert.Equal("gpt-5.6-luna", response.DefaultModelId);
        Assert.Equal(2, response.Models.Count);
        Assert.True(response.Models[0].IsDefault);
        Assert.Equal("Luna", response.Models[0].DisplayName);
        Assert.False(response.Models[1].IsDefault);
        Assert.Equal(organization.Id, policyReader.ReceivedOrganizationId);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoActiveModel_When_GetAvailableModelsAsync_Then_Throws(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var policy = new ModelPolicy("gpt-5.6-luna", []);
        var service = new ModelCatalogService(
            new StubUserContextService(new MessageUserContext(organization, member)),
            new StubModelPolicyReader(policy));

        // When / Then
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAvailableModelsAsync(CancellationToken.None));
    }

    [Theory, AutoDomainData]
    public async Task Given_ADefaultModelNotInTheActiveList_When_GetAvailableModelsAsync_Then_Throws(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var policy = new ModelPolicy(
            "gpt-5.6-unknown",
            [new ModelPolicyEntry("gpt-5.6-luna", "Luna", "Modele general.")]);
        var service = new ModelCatalogService(
            new StubUserContextService(new MessageUserContext(organization, member)),
            new StubModelPolicyReader(policy));

        // When / Then
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAvailableModelsAsync(CancellationToken.None));
    }

    private sealed class StubUserContextService(MessageUserContext context)
        : IMessageUserContextService
    {
        public Task<MessageUserContext> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(context);
    }

    private sealed class StubModelPolicyReader(ModelPolicy policy) : IModelPolicyReader
    {
        public Guid? ReceivedOrganizationId { get; private set; }

        public Task<ModelPolicy> GetPolicyAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            ReceivedOrganizationId = organizationId;
            return Task.FromResult(policy);
        }
    }
}

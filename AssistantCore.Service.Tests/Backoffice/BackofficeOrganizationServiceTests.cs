using AssistantCore.Repository.Queries;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Backoffice;

namespace AssistantCore.Service.Tests.Backoffice;

public sealed class BackofficeOrganizationServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_InvalidPagination_When_SearchOrganizationsAsync_Then_NormalizesPagination(
        CancellationToken cancellationToken)
    {
        // Given
        var queries = new StubBackofficeOrganizationQueries
        {
            ListResponse = new BackofficeOrganizationListPageData([], 1, 25, 0)
        };
        var service = new BackofficeOrganizationService(queries);

        // When
        var result = await service.SearchOrganizationsAsync(
            0,
            0,
            "metal",
            cancellationToken);

        // Then
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(1, queries.ReceivedPage);
        Assert.Equal(25, queries.ReceivedPageSize);
        Assert.Equal("metal", queries.ReceivedSearch);
        Assert.Equal(cancellationToken, queries.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_PageSizeAboveMaximum_When_SearchOrganizationsAsync_Then_CapsPageSize(
        CancellationToken cancellationToken)
    {
        // Given
        var queries = new StubBackofficeOrganizationQueries
        {
            ListResponse = new BackofficeOrganizationListPageData([], 1, 100, 0)
        };
        var service = new BackofficeOrganizationService(queries);

        // When
        var result = await service.SearchOrganizationsAsync(
            1,
            250,
            null,
            cancellationToken);

        // Then
        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, queries.ReceivedPageSize);
        Assert.Equal(cancellationToken, queries.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_ExistingOrganization_When_GetOrganizationDetailsAsync_Then_ReturnsDetails(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        // Given
        var details = new BackofficeOrganizationDetailsData(
            new BackofficeOrganizationData(
                organizationId,
                "MetalPro",
                "tenant-metal",
                RecordStatus.Active,
                DateTimeOffset.Parse("2026-09-10T20:00:00Z")),
            new BackofficeOrganizationUsersData(3, 2),
            new BackofficeOrganizationMicrosoftData(true, true),
            new BackofficeOrganizationSourcesData(1, 1),
            new BackofficeOrganizationIndexingData(
                42,
                DateTimeOffset.Parse("2026-09-10T21:00:00Z"),
                "Healthy"));
        var queries = new StubBackofficeOrganizationQueries { DetailsResponse = details };
        var service = new BackofficeOrganizationService(queries);

        // When
        var result = await service.GetOrganizationDetailsAsync(
            organizationId,
            cancellationToken);

        // Then
        Assert.Equal(organizationId, result.Organization.Id);
        Assert.Equal("MetalPro", result.Organization.Name);
        Assert.Equal("tenant-metal", result.Organization.TenantId);
        Assert.Equal("Active", result.Organization.Status);
        Assert.Equal(3, result.Users.Total);
        Assert.Equal(2, result.Users.Active);
        Assert.True(result.Microsoft.Connected);
        Assert.True(result.Microsoft.AdminConsentGranted);
        Assert.Equal(1, result.Sources.SharePointSiteCount);
        Assert.Equal(1, result.Sources.OneDriveCount);
        Assert.Equal(42, result.Indexing.DocumentCount);
        Assert.Equal("Healthy", result.Indexing.Status);
        Assert.Equal(organizationId, queries.ReceivedOrganizationId);
        Assert.Equal(cancellationToken, queries.ReceivedCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_EmptyOrganizationId_When_GetOrganizationDetailsAsync_Then_ThrowsBadRequest(
        int _)
    {
        // Given
        var service = new BackofficeOrganizationService(new StubBackofficeOrganizationQueries());

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => service.GetOrganizationDetailsAsync(Guid.Empty, CancellationToken.None));

        // Then
        Assert.Equal("Organization identifier is required.", exception.Message);
    }

    [Theory, AutoDomainData]
    public async Task Given_MissingOrganization_When_GetOrganizationDetailsAsync_Then_ThrowsNotFound(
        Guid organizationId)
    {
        // Given
        var service = new BackofficeOrganizationService(new StubBackofficeOrganizationQueries());

        // When
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetOrganizationDetailsAsync(organizationId, CancellationToken.None));

        // Then
        Assert.Equal("Organization not found.", exception.Message);
    }

    private sealed class StubBackofficeOrganizationQueries : IBackofficeOrganizationQueries
    {
        public BackofficeOrganizationListPageData? ListResponse { get; init; }

        public BackofficeOrganizationDetailsData? DetailsResponse { get; init; }

        public int ReceivedPage { get; private set; }

        public int ReceivedPageSize { get; private set; }

        public string? ReceivedSearch { get; private set; }

        public Guid ReceivedOrganizationId { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<BackofficeOrganizationListPageData> SearchOrganizationsAsync(
            int page,
            int pageSize,
            string? search,
            CancellationToken cancellationToken = default)
        {
            ReceivedPage = page;
            ReceivedPageSize = pageSize;
            ReceivedSearch = search;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(ListResponse!);
        }

        public Task<BackofficeOrganizationDetailsData?> GetOrganizationDetailsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrganizationId = organizationId;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(DetailsResponse);
        }
    }
}

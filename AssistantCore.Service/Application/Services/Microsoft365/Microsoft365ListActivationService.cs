using System.Diagnostics;
using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365ListActivationService(
    IAuthenticateUserService authenticateUserService,
    IMicrosoft365SourceDiscoveryRepository sourceDiscoveryRepository,
    TimeProvider timeProvider,
    ILogger<Microsoft365ListActivationService> logger) : IMicrosoft365ListActivationService
{
    public async Task<Microsoft365List> SetIndexingAsync(
        string siteId,
        string listId,
        bool isIndexed,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        if (member.Role != OrganizationRole.Admin)
        {
            throw new ForbiddenException("Administrator access required.");
        }

        var site = await sourceDiscoveryRepository.FindSiteAsync(
            organization.Id,
            siteId,
            cancellationToken)
            ?? throw new NotFoundException("Microsoft 365 site was not found.");

        var list = await sourceDiscoveryRepository.FindListAsync(
            organization.Id,
            siteId,
            listId,
            cancellationToken)
            ?? throw new NotFoundException("Microsoft 365 list was not found.");

        if (list.Microsoft365ConnectionId != site.Microsoft365ConnectionId
            || list.OrganizationConnectorId != site.OrganizationConnectorId)
        {
            throw new NotFoundException("Microsoft 365 list was not found.");
        }

        if (site.Status != Microsoft365SourceStatus.Enabled)
        {
            throw new BadRequestException("Microsoft 365 site is not enabled.");
        }

        if (site.Microsoft365Connection.Status != Microsoft365ConnectionStatus.Active
            || site.OrganizationConnector.Status != RecordStatus.Active
            || !site.OrganizationConnector.IsConfigured)
        {
            throw new BadRequestException("Microsoft 365 connector is not active.");
        }

        if (list.Status == Microsoft365SourceStatus.Unavailable)
        {
            throw new BadRequestException("Microsoft 365 list is unavailable.");
        }

        var requestedAt = timeProvider.GetUtcNow();
        Microsoft365ListIndexingRequestCounts requestCounts;
        if (isIndexed)
        {
            EnsureDeactivationIsComplete(list);
            if (!list.EnableIndexing(requestedAt))
            {
                requestCounts = Microsoft365ListIndexingRequestCounts.Empty;
            }
            else
            {
                requestCounts = await sourceDiscoveryRepository.SaveListActivationAsync(
                    list,
                    requestedAt,
                    cancellationToken);
            }
        }
        else
        {
            var requestIndexCleanup = list.IsIndexed;
            if (!list.DisableIndexing())
            {
                requestCounts = Microsoft365ListIndexingRequestCounts.Empty;
            }
            else
            {
                requestCounts = await sourceDiscoveryRepository.SaveListDeactivationAsync(
                    list,
                    requestedAt,
                    requestIndexCleanup,
                    cancellationToken);
            }
        }

        var duration = Stopwatch.GetElapsedTime(startedAt);
        logger.LogInformation(
            "Microsoft 365 list indexing updated. OrganizationId: {OrganizationId}; SiteId: {SiteId}; ListId: {ListId}; IsIndexed: {IsIndexed}; InitialSynchronizationRequests: {InitialSynchronizationRequests}; CancelledIngestionJobs: {CancelledIngestionJobs}; SubscriptionCreationRequests: {SubscriptionCreationRequests}; SubscriptionStopRequests: {SubscriptionStopRequests}; IndexCleanupRequests: {IndexCleanupRequests}; DurationMs: {DurationMs}.",
            organization.Id,
            siteId,
            listId,
            list.IsIndexed,
            requestCounts.InitialSynchronizationRequests,
            requestCounts.CancelledIngestionJobs,
            requestCounts.SubscriptionCreationRequests,
            requestCounts.SubscriptionStopRequests,
            requestCounts.IndexCleanupRequests,
            duration.TotalMilliseconds);

        return list;
    }

    private static void EnsureDeactivationIsComplete(Microsoft365List list)
    {
        var cleanupInProgress = list.Synchronizations.Any(synchronization =>
            synchronization.Type == Microsoft365SynchronizationType.IndexCleanup
            && synchronization.Status is Microsoft365SynchronizationStatus.Pending
                or Microsoft365SynchronizationStatus.Running
                or Microsoft365SynchronizationStatus.TemporaryFailure);
        var subscriptionRevocationInProgress = list.Subscriptions.Any(subscription =>
            subscription.Status == Microsoft365SubscriptionStatus.RevocationRequired);

        if (cleanupInProgress || subscriptionRevocationInProgress)
        {
            throw new BadRequestException("Microsoft 365 list deactivation is still in progress.");
        }
    }
}

using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using Microsoft.Extensions.Caching.Memory;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365ResetService(
    IAuthenticateUserService authenticateUserService,
    IMicrosoft365ConnectionRepository connectionRepository,
    IMicrosoft365ResetRepository resetRepository,
    IMicrosoft365PassageIndexWriter indexWriter,
    IMemoryCache memoryCache) : IMicrosoft365ResetService
{
    public async Task<Microsoft365ResetResult> ResetSelectionAndIndexingAsync(
        CancellationToken cancellationToken = default)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        if (member.Role != OrganizationRole.Admin)
        {
            throw new ForbiddenException("Administrator access required.");
        }

        // L'organisation vient du contexte authentifie, jamais d'un parametre de
        // requete : un administrateur ne peut reinitialiser que sa propre organisation.
        var connection = await connectionRepository.FindByOrganizationAsync(
            organization.Id,
            cancellationToken)
            ?? throw new NotFoundException("Microsoft 365 connection not found.");

        if (connection.Status != Microsoft365ConnectionStatus.Active)
        {
            throw new ConflictException(
                "The Microsoft 365 connection must be active to reset the selection.",
                ConflictException.Microsoft365ConnectionInactive);
        }

        // Les chunks sont lus et supprimes de l'index AVANT les lignes qui les
        // decrivent. Dans l'ordre inverse, un echec laisserait des documents
        // orphelins dans Azure AI Search, sans plus aucun moyen de les retrouver
        // ni donc de les supprimer.
        var chunkIds = await resetRepository.GetIndexedChunkIdsAsync(
            organization.Id,
            cancellationToken);

        if (chunkIds.Count > 0)
        {
            await indexWriter.DeleteAsync(chunkIds, cancellationToken);
        }

        var counts = await resetRepository.ResetSelectionAndIndexingAsync(
            organization.Id,
            cancellationToken);

        // Le cache d'onboarding retient l'etat "setup termine" pendant 30 secondes.
        // Sans invalidation, l'administrateur reverrait son organisation comme
        // configuree juste apres avoir demande sa reinitialisation.
        memoryCache.Remove(Microsoft365OnboardingCacheKeys.CompletionKey(organization.Id));

        return new Microsoft365ResetResult(
            organization.Id,
            counts.Subscriptions,
            counts.Synchronizations,
            counts.DocumentWorks + counts.ListItemWorks,
            counts.IndexedContents,
            counts.Sources,
            chunkIds.Count);
    }
}

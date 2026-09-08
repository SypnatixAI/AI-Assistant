using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class Microsoft365ResetRepository(AssistantCoreDbContext dbContext)
    : IMicrosoft365ResetRepository
{
    public async Task<IReadOnlyCollection<string>> GetIndexedChunkIdsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Microsoft365IndexedPassages
            .AsNoTracking()
            .Where(passage =>
                passage.Microsoft365IndexedContent.OrganizationId == organizationId)
            .Select(passage => passage.ChunkId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    public async Task<Microsoft365ResetCounts> ResetSelectionAndIndexingAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        // Microsoft365Synchronization et Microsoft365IndexedPassage ne portent pas
        // d'OrganizationId : leur rattachement passe par la source ou le contenu.
        // Les identifiants de source sont donc resolus une fois, depuis
        // l'organisation, et servent de perimetre a toutes les suppressions qui en
        // dependent. Aucune suppression ne s'appuie sur un identifiant fourni par
        // l'appelant.
        var sourceIds = await dbContext.Microsoft365Sources
            .AsNoTracking()
            .Where(source => source.Microsoft365Connection.OrganizationId == organizationId)
            .Select(source => source.Id)
            .ToArrayAsync(cancellationToken);

        // L'ordre va des enfants vers les parents. Les travaux portent une clef
        // etrangere obligatoire vers leur synchronisation : supprimer les
        // synchronisations d'abord romprait une relation requise et ferait echouer
        // l'enregistrement.
        var documentWorks = await dbContext.Microsoft365DocumentWorks
            .Where(work => work.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        dbContext.Microsoft365DocumentWorks.RemoveRange(documentWorks);

        var listItemWorks = await dbContext.Microsoft365ListItemWorks
            .Where(work => work.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        dbContext.Microsoft365ListItemWorks.RemoveRange(listItemWorks);

        // Les passages sont supprimes explicitement plutot que par cascade : le
        // comportement d'une cascade depend du fournisseur, et un passage orphelin
        // laisserait croire qu'un chunk existe encore dans l'index.
        var indexedContents = await dbContext.Microsoft365IndexedContents
            .Include(content => content.Passages)
            .Where(content => content.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        foreach (var content in indexedContents)
        {
            dbContext.Microsoft365IndexedPassages.RemoveRange(content.Passages);
        }

        dbContext.Microsoft365IndexedContents.RemoveRange(indexedContents);

        var subscriptions = await dbContext.Microsoft365Subscriptions
            .Where(subscription => subscription.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        dbContext.Microsoft365Subscriptions.RemoveRange(subscriptions);

        var synchronizations = await dbContext.Microsoft365Synchronizations
            .Where(synchronization => sourceIds.Contains(synchronization.Microsoft365SourceId))
            .ToListAsync(cancellationToken);
        dbContext.Microsoft365Synchronizations.RemoveRange(synchronizations);

        // Les sources partent en dernier : supprimer les sites, lecteurs et listes
        // est ce qui fait retourner une collection vide a GetSiteIdsAsync, donc ce
        // qui replace l'administrateur devant une selection vierge.
        var sources = await dbContext.Microsoft365Sources
            .Where(source => source.Microsoft365Connection.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        dbContext.Microsoft365Sources.RemoveRange(sources);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new Microsoft365ResetCounts(
            subscriptions.Count,
            synchronizations.Count,
            documentWorks.Count,
            listItemWorks.Count,
            indexedContents.Count,
            sources.Count);
    }
}

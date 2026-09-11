using System.Runtime.CompilerServices;
using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365DriveItemDeltaClientAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphDriveItemDeltaClient graphClient,
    MicrosoftGraphSharedDriveItemSearchClient sharedItemSearchClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365DriveItemDeltaClient
{
    public IAsyncEnumerable<Microsoft365DriveItemDeltaPage> GetInitialPagesAsync(
        string tenantId,
        string driveId,
        CancellationToken cancellationToken = default) =>
        GetPagesWithSharedItemsAsync(
            tenantId,
            driveId,
            (configuration, accessToken) => graphClient.GetInitialPagesAsync(
                configuration.GraphBaseUrl,
                accessToken,
                driveId,
                cancellationToken),
            cancellationToken);

    public IAsyncEnumerable<Microsoft365DriveItemDeltaPage> GetDeltaPagesAsync(
        string tenantId,
        string deltaLink,
        CancellationToken cancellationToken = default)
    {
        var driveId = TryReadDriveIdFromDeltaLink(deltaLink)
            ?? throw new ArgumentException(
                "The Microsoft Graph delta link does not identify a drive.",
                nameof(deltaLink));

        return GetPagesWithSharedItemsAsync(
            tenantId,
            driveId,
            (configuration, accessToken) => graphClient.GetDeltaPagesAsync(
                configuration.GraphBaseUrl,
                accessToken,
                deltaLink,
                cancellationToken),
            cancellationToken);
    }

    private async IAsyncEnumerable<Microsoft365DriveItemDeltaPage> GetPagesWithSharedItemsAsync(
        string tenantId,
        string driveId,
        Func<Microsoft365Options, string,
            IAsyncEnumerable<AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftDriveItemDeltaPage>>
            createPages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftAuthorizationCodeToken token;
        try
        {
            token = await identityClient.AcquireApplicationTokenAsync(
                configuration.AuthorityBaseUrl,
                tenantId,
                configuration.ClientId,
                configuration.ClientSecret,
                cancellationToken);
        }
        catch (MicrosoftExternalException exception)
        {
            throw CreateApplicationException(exception);
        }

        await foreach (var page in ReadDeltaPagesAsync(
                           createPages(configuration, token.AccessToken),
                           cancellationToken))
        {
            yield return page;
        }

        var sharedItems = new List<Microsoft365DriveItemDelta>();
        try
        {
            await foreach (var item in sharedItemSearchClient.GetSharedItemsAsync(
                               configuration.GraphBaseUrl,
                               token.AccessToken,
                               driveId,
                               cancellationToken))
            {
                sharedItems.Add(MapItem(item));
            }
        }
        catch (MicrosoftExternalException exception)
        {
            throw CreateApplicationException(exception);
        }

        if (sharedItems.Count > 0)
        {
            // The shared discovery page deliberately has no delta link. The
            // synchronization service retains the checkpoint returned by the
            // regular drive delta pages while processing these canonical items.
            yield return new Microsoft365DriveItemDeltaPage(sharedItems, DeltaLink: null);
        }
    }

    private async IAsyncEnumerable<Microsoft365DriveItemDeltaPage> ReadDeltaPagesAsync(
        IAsyncEnumerable<AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftDriveItemDeltaPage> pages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = pages.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasPage;
            try
            {
                hasPage = await enumerator.MoveNextAsync();
            }
            catch (MicrosoftExternalException exception)
            {
                throw CreateApplicationException(exception);
            }

            if (!hasPage)
            {
                yield break;
            }

            var page = enumerator.Current;
            yield return new Microsoft365DriveItemDeltaPage(
                page.Items.Select(MapItem).ToArray(),
                page.DeltaLink);
        }
    }

    private static Microsoft365DriveItemDelta MapItem(
        AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftDriveItemDelta item) =>
        new(
            item.Id,
            item.Name,
            item.ETag,
            item.CreatedDateTime,
            item.LastModifiedDateTime,
            item.WebUrl,
            item.Size,
            item.MimeType,
            item.IsDeleted,
            item.IsFolder,
            item.IsFile)
        {
            CanonicalDriveId = item.CanonicalDriveId
        };

    private static string? TryReadDriveIdFromDeltaLink(string deltaLink)
    {
        if (!Uri.TryCreate(deltaLink, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (string.Equals(segments[index], "drives", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(segments[index + 1]);
            }
        }

        return null;
    }

    private static Exception CreateApplicationException(
        MicrosoftExternalException exception) =>
        IsAccessDenied(exception)
            ? new Microsoft365SourceAccessDeniedException(
                "Microsoft 365 drive source access was denied.",
                exception)
            : IsInvalidDeltaCheckpoint(exception)
            ? new Microsoft365DeltaCheckpointInvalidException(
                "Microsoft 365 drive item delta checkpoint is invalid.",
                exception)
            : IsTransient(exception)
                ? new Microsoft365GraphTransientException(
                    "Microsoft 365 drive item delta failed temporarily.",
                    exception)
                : new Microsoft365ExternalException("Microsoft 365 drive item delta could not be loaded.", exception);

    private static bool IsAccessDenied(MicrosoftExternalException exception) =>
        exception.StatusCode == HttpStatusCode.Forbidden;

    private static bool IsInvalidDeltaCheckpoint(MicrosoftExternalException exception) =>
        exception.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound
        || IsInvalidDeltaCheckpointErrorCode(exception.ErrorCode);

    private static bool IsInvalidDeltaCheckpointErrorCode(string? errorCode) =>
        string.Equals(errorCode, "syncStateNotFound", StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorCode, "resyncRequired", StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorCode, "resyncChangesApplyDifferences", StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorCode, "resyncChangesUploadDifferences", StringComparison.OrdinalIgnoreCase);

    private static bool IsTransient(MicrosoftExternalException exception) =>
        exception.StatusCode == HttpStatusCode.TooManyRequests
        || (exception.StatusCode.HasValue && (int)exception.StatusCode.Value >= 500);
}

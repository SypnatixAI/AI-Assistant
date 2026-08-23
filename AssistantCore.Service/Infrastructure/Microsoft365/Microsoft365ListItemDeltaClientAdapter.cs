using System.Runtime.CompilerServices;
using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365ListItemDeltaClientAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphListItemDeltaClient graphClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365ListItemDeltaClient
{
    public IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetInitialPagesAsync(
        string tenantId,
        string siteId,
        string listId,
        CancellationToken cancellationToken = default) =>
        GetPagesAsync(
            tenantId,
            (configuration, accessToken) => graphClient.GetInitialPagesAsync(
                configuration.GraphBaseUrl,
                accessToken,
                siteId,
                listId,
                cancellationToken),
            cancellationToken);

    public IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetDeltaPagesAsync(
        string tenantId,
        string deltaLink,
        CancellationToken cancellationToken = default) =>
        GetPagesAsync(
            tenantId,
            (configuration, accessToken) => graphClient.GetDeltaPagesAsync(
                configuration.GraphBaseUrl,
                accessToken,
                deltaLink,
                cancellationToken),
            cancellationToken);

    private async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetPagesAsync(
        string tenantId,
        Func<Microsoft365Options, string,
            IAsyncEnumerable<AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftListItemDeltaPage>>
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

        var pages = createPages(configuration, token.AccessToken);
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
            yield return new Microsoft365ListItemDeltaPage(
                page.Items.Select(item => new Microsoft365ListItemDelta(
                    item.Id,
                    item.ETag,
                    item.CreatedDateTime,
                    item.LastModifiedDateTime,
                    item.WebUrl,
                    item.Fields?.Clone(),
                    item.IsDeleted)).ToArray(),
                page.DeltaLink);
        }
    }

    private static Exception CreateApplicationException(
        MicrosoftExternalException exception) =>
        IsAccessDenied(exception)
            ? new Microsoft365SourceAccessDeniedException(
                "Microsoft 365 list source access was denied.",
                exception)
            : IsInvalidDeltaCheckpoint(exception)
            ? new Microsoft365DeltaCheckpointInvalidException(
                "Microsoft 365 list item delta checkpoint is invalid.",
                exception)
            : IsTransient(exception)
                ? new Microsoft365GraphTransientException(
                    "Microsoft 365 list item delta failed temporarily.",
                    exception)
                : new Microsoft365ExternalException("Microsoft 365 list item delta could not be loaded.", exception);

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

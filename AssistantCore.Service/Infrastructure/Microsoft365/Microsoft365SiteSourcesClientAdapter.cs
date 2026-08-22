using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365SiteSourcesClientAdapter(
    MicrosoftGraphSiteSourcesClient graphClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365SiteSourcesClient
{
    private const string DocumentLibraryTemplate = "documentLibrary";

    public async Task<Microsoft365SiteSourcesDiscoveryResult> GetSiteSourcesAsync(
        string accessToken,
        string siteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var drivesTask = graphClient.GetSiteDrivesAsync(
                options.Value.GraphBaseUrl,
                accessToken,
                siteId,
                cancellationToken);
            var listsTask = graphClient.GetSiteListsAsync(
                options.Value.GraphBaseUrl,
                accessToken,
                siteId,
                cancellationToken);

            await Task.WhenAll(drivesTask, listsTask);

            var drives = (await drivesTask)
                .Select(drive => new Microsoft365DiscoveredDrive(
                    siteId,
                    drive.Id,
                    drive.Name,
                    drive.WebUrl))
                .ToArray();
            var lists = (await listsTask)
                .Where(IsContentList)
                .Select(list => new Microsoft365DiscoveredList(
                    siteId,
                    list.Id,
                    list.DisplayName,
                    list.WebUrl))
                .ToArray();

            return Microsoft365SiteSourcesDiscoveryResult.Succeeded(
                new Microsoft365DiscoveredSiteSources(drives, lists));
        }
        catch (MicrosoftExternalException exception)
            when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return Microsoft365SiteSourcesDiscoveryResult.Forbidden();
        }
        catch (MicrosoftExternalException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return Microsoft365SiteSourcesDiscoveryResult.SiteNotFound();
        }
        catch (MicrosoftExternalException exception)
            when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Microsoft365SiteSourcesDiscoveryResult.Throttled(
                exception.RetryAfterDelay,
                exception.RetryAfterAt);
        }
        catch (MicrosoftExternalException exception)
        {
            throw new Microsoft365ExternalException(
                "Microsoft 365 site sources could not be discovered.",
                exception);
        }
    }

    private static bool IsContentList(
        AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftList list) =>
        !string.Equals(
            list.Template,
            DocumentLibraryTemplate,
            StringComparison.OrdinalIgnoreCase)
        && !list.IsHidden
        && !list.IsSystem
        && !list.IsDeleted;
}

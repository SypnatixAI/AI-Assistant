using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tabular;
using AssistantCore.Service.Application.Services.Messages.Tabular;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365SpreadsheetDocumentResolver(
    IMicrosoft365DelegatedTokenProvider tokenProvider,
    MicrosoftGraphDriveItemSearchClient searchClient,
    MicrosoftGraphDriveContentClient contentClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365SpreadsheetDocumentResolver
{
    public async Task<Microsoft365SpreadsheetDocument> ResolveAsync(
        string fileName,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(context);
        EnsureMicrosoftIdentity(context);

        var extension = Path.GetExtension(fileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only XLSX and XLSM workbooks can be resolved.", nameof(fileName));
        }

        var configuration = options.Value;
        var accessToken = await tokenProvider.GetGraphAccessTokenAsync(
            context.ExternalTenantId!,
            cancellationToken);
        var results = await searchClient.SearchAsync(
            configuration.GraphBaseUrl,
            accessToken,
            fileName,
            cancellationToken);

        var matches = results
            .Where(result => string.Equals(result.Name, fileName, StringComparison.OrdinalIgnoreCase))
            .GroupBy(result => $"{result.DriveId}\u001f{result.DriveItemId}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                $"Spreadsheet '{fileName}' was not found or is not accessible to the current user.");
        }

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Several accessible spreadsheets are named '{fileName}'; use a unique file name.");
        }

        var match = matches.Single();
        var content = await contentClient.DownloadAsync(
            configuration.GraphBaseUrl,
            accessToken,
            match.DriveId,
            match.DriveItemId,
            configuration.MaximumExtractionFileSizeBytes,
            cancellationToken);

        return new Microsoft365SpreadsheetDocument(
            match.Name,
            content,
            $"m365://drive/{match.DriveId}/item/{match.DriveItemId}",
            match.WebUrl);
    }

    private static void EnsureMicrosoftIdentity(ConnectorExecutionContext context)
    {
        if (context.OrganizationId == Guid.Empty
            || context.MemberId == Guid.Empty
            || context.IdentityProvider != IdentityProvider.MicrosoftEntraId
            || string.IsNullOrWhiteSpace(context.ExternalTenantId)
            || context.EntraUserId is null
            || context.EntraUserId == Guid.Empty
            || string.IsNullOrWhiteSpace(context.UserEmail))
        {
            throw new InvalidOperationException(
                "The authenticated member cannot be resolved to a Microsoft Entra identity.");
        }
    }
}

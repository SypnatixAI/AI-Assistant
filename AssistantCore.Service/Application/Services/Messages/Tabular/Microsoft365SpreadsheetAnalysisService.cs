using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tabular;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Services.Messages.Tabular;

public sealed class Microsoft365SpreadsheetAnalysisService(
    IMicrosoft365SpreadsheetDocumentResolver documentResolver,
    IMicrosoft365DriveContentClient contentClient,
    ISpreadsheetWorkbookReader workbookReader,
    ISpreadsheetAnalysisEngine analysisEngine) : IMicrosoft365SpreadsheetAnalysisService
{
    private static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>([".xlsx", ".xlsm"], StringComparer.OrdinalIgnoreCase);

    public async Task<SpreadsheetAnalysisResult> AnalyzeAsync(
        SpreadsheetAnalysisRequest request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        if (!SupportedExtensions.Contains(Path.GetExtension(request.FileName)))
        {
            throw new ArgumentException("Only XLSX and XLSM workbooks can be analyzed.");
        }

        EnsureMicrosoftIdentity(context);

        var document = await documentResolver.ResolveAsync(
            request.FileName,
            context,
            cancellationToken);
        var content = await contentClient.DownloadAsync(
            document.TenantId,
            document.DriveId,
            document.DriveItemId,
            cancellationToken);
        var workbook = await workbookReader.ReadAsync(
            document.FileName,
            content,
            cancellationToken);
        var computation = analysisEngine.Analyze(workbook, request);

        return new SpreadsheetAnalysisResult(
            document.FileName,
            computation.WorksheetName,
            computation.TotalRowCount,
            computation.Aggregations,
            computation.MatchingRowCount,
            computation.Rows,
            computation.RowsTruncated,
            document.Reference,
            document.Url);
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

using System.IO.Compression;
using System.Xml;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftExcelTableReaderClient
{
    private readonly MicrosoftExcelWorksheetReader worksheetReader = new();

    public async Task<MicrosoftExcelTableWorkbook> ReadAsync(
        string fileName,
        byte[] content,
        long maximumExpandedSizeBytes,
        int maximumSheets,
        int maximumCells,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        if (!MicrosoftExcelPackageFormat.SupportedExtensions.Contains(Path.GetExtension(fileName)))
        {
            throw new InvalidDataException("Only XLSX and XLSM workbooks can be analyzed.");
        }

        using var contentStream = new MemoryStream(content, writable: false);
        using var package = new ZipArchive(contentStream, ZipArchiveMode.Read);
        if (ExceedsExpandedSizeLimit(package.Entries, maximumExpandedSizeBytes))
        {
            throw new InvalidDataException("The expanded workbook exceeds the configured size limit.");
        }

        var workbookEntry = FindEntry(package, MicrosoftExcelPackageFormat.WorkbookPath)
            ?? throw new InvalidDataException("The workbook definition is missing.");
        var relationshipEntry = FindEntry(package, MicrosoftExcelPackageFormat.WorkbookRelationshipsPath)
            ?? throw new InvalidDataException("The workbook relationships are missing.");
        var sharedStrings = await worksheetReader.ReadSharedStringsAsync(
            FindEntry(package, MicrosoftExcelPackageFormat.SharedStringsPath),
            cancellationToken);
        var dateStyles = await worksheetReader.ReadDateStylesAsync(
            FindEntry(package, MicrosoftExcelPackageFormat.StylesPath),
            cancellationToken);
        var workbook = await worksheetReader.LoadXmlAsync(workbookEntry, cancellationToken);
        var relationships = await worksheetReader.LoadXmlAsync(relationshipEntry, cancellationToken);
        var sheetTargets = relationships
            .Elements(MicrosoftExcelPackageFormat.PackageRelationshipsNamespace + "Relationship")
            .ToDictionary(
                element => (string)element.Attribute("Id")!,
                element => ResolvePath("xl", (string)element.Attribute("Target")!),
                StringComparer.Ordinal);

        var worksheets = new List<MicrosoftExcelTableWorksheet>();
        var state = new MicrosoftExcelExtractionState();
        var visibleSheetCount = 0;
        foreach (var sheet in workbook.Descendants(MicrosoftExcelPackageFormat.SpreadsheetNamespace + "sheet"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((string?)sheet.Attribute("state") is "hidden" or "veryHidden")
            {
                continue;
            }

            if (++visibleSheetCount > maximumSheets)
            {
                throw new InvalidDataException("The workbook exceeds the configured worksheet limit.");
            }

            var relationshipId = (string?)sheet.Attribute(
                MicrosoftExcelPackageFormat.OfficeRelationshipsNamespace + "id");
            if (relationshipId is null || !sheetTargets.TryGetValue(relationshipId, out var target))
            {
                throw new XmlException("A worksheet relationship is invalid.");
            }

            var entry = FindEntry(package, target)
                ?? throw new InvalidDataException("A worksheet definition is missing.");
            var sourceRows = await worksheetReader.ReadRowsAsync(
                entry,
                sharedStrings,
                dateStyles,
                maximumCells,
                state,
                new HashSet<MicrosoftExcelExtractionWarning>(),
                cancellationToken);
            var table = CreateTable((string?)sheet.Attribute("name") ?? string.Empty, sourceRows);
            if (table is not null)
            {
                worksheets.Add(table);
            }
        }

        return worksheets.Count > 0
            ? new MicrosoftExcelTableWorkbook(worksheets)
            : throw new InvalidDataException("The workbook contains no analyzable table.");
    }

    private static MicrosoftExcelTableWorksheet? CreateTable(
        string sheetName,
        IReadOnlyList<MicrosoftExcelWorksheetRow> rows)
    {
        var headerIndex = rows
            .Select((row, index) => new { row, index })
            .FirstOrDefault(item => item.row.Cells.Count >= 2)?.index;
        if (headerIndex is null)
        {
            return null;
        }

        var headerCells = rows[headerIndex.Value].Cells;
        var headers = new Dictionary<int, string>();
        var usedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerCells)
        {
            var header = cell.Value.Trim();
            if (string.IsNullOrWhiteSpace(header) || !usedHeaders.Add(header))
            {
                throw new InvalidDataException(
                    $"Worksheet '{sheetName}' contains an empty or duplicate column name.");
            }

            headers.Add(cell.ColumnIndex, header);
        }

        var tableRows = rows
            .Skip(headerIndex.Value + 1)
            .Select(row => headers.ToDictionary(
                header => header.Value,
                header => row.Cells.FirstOrDefault(cell => cell.ColumnIndex == header.Key)?.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase))
            .Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .Cast<IReadOnlyDictionary<string, string>>()
            .ToArray();

        return new MicrosoftExcelTableWorksheet(
            sheetName,
            headers.Values.ToArray(),
            tableRows);
    }

    private static bool ExceedsExpandedSizeLimit(
        IReadOnlyCollection<ZipArchiveEntry> entries,
        long maximumBytes)
    {
        long total = 0;
        foreach (var entry in entries)
        {
            if (entry.Length > maximumBytes - total)
            {
                return true;
            }

            total += entry.Length;
        }

        return false;
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive package, string path) =>
        package.Entries.FirstOrDefault(entry =>
            entry.FullName.Equals(path.TrimStart('/'), StringComparison.OrdinalIgnoreCase));

    private static string ResolvePath(string basePath, string target)
    {
        var path = target.StartsWith('/') ? target.TrimStart('/') : $"{basePath}/{target}";
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part == ".." && parts.Count > 0)
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }
}

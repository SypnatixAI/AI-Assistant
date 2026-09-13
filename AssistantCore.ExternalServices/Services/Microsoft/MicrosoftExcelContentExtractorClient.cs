using System.IO.Compression;
using System.Xml.Linq;
using System.Xml;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftExcelContentExtractorClient
{
    private static readonly byte[] CompoundDocumentSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private readonly MicrosoftExcelWorksheetReader worksheetReader = new();
    public async Task<MicrosoftExcelExtractionResult> ExtractAsync(
        string fileName, string? mimeType, Stream content, long? contentLength,
        MicrosoftExcelExtractionLimits limits, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        var extension = Path.GetExtension(fileName);
        if (!MicrosoftExcelPackageFormat.SupportedExtensions.Contains(extension))
            return Result(MicrosoftExcelExtractionStatus.UnsupportedFormat);
        if (!string.IsNullOrWhiteSpace(mimeType)
            && !MicrosoftExcelPackageFormat.SupportedMimeTypes.Contains(mimeType.Split(';', 2)[0].Trim()))
            return Result(MicrosoftExcelExtractionStatus.UnsupportedFormat);
        if (contentLength is < 0 || contentLength > limits.MaximumFileSizeBytes)
            return Result(MicrosoftExcelExtractionStatus.TooLarge);

        cancellationToken.ThrowIfCancellationRequested();
        await using var packageStream = await CopyWithinLimitAsync(content, limits.MaximumFileSizeBytes, cancellationToken);
        if (packageStream is null) return Result(MicrosoftExcelExtractionStatus.TooLarge);
        if (HasSignature(packageStream, CompoundDocumentSignature)) return Result(MicrosoftExcelExtractionStatus.EncryptedWorkbook);
        if (!HasZipSignature(packageStream)) return Result(MicrosoftExcelExtractionStatus.CorruptedWorkbook);

        try
        {
            using var package = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
            if (ExceedsExpandedSizeLimit(package.Entries, limits.MaximumExpandedSizeBytes))
                return Result(MicrosoftExcelExtractionStatus.TooLarge);
            var workbookEntry = FindEntry(package, MicrosoftExcelPackageFormat.WorkbookPath);
            var workbookRelationships = FindEntry(package, MicrosoftExcelPackageFormat.WorkbookRelationshipsPath);
            if (workbookEntry is null || workbookRelationships is null)
                return Result(MicrosoftExcelExtractionStatus.CorruptedWorkbook);

            var sharedStrings = await worksheetReader.ReadSharedStringsAsync(FindEntry(package, MicrosoftExcelPackageFormat.SharedStringsPath), cancellationToken);
            var styles = await worksheetReader.ReadDateStylesAsync(FindEntry(package, MicrosoftExcelPackageFormat.StylesPath), cancellationToken);
            var workbook = await worksheetReader.LoadXmlAsync(workbookEntry, cancellationToken);
            var relationships = await worksheetReader.LoadXmlAsync(workbookRelationships, cancellationToken);
            var sheetTargets = relationships.Elements(MicrosoftExcelPackageFormat.PackageRelationshipsNamespace + "Relationship")
                .ToDictionary(e => (string)e.Attribute("Id")!, e => ResolvePath("xl", (string)e.Attribute("Target")!), StringComparer.Ordinal);
            var warnings = DetectWarnings(package);
            var units = new List<MicrosoftExcelExtractedUnit>();
            var sheetCount = 0;
            var extractionState = new MicrosoftExcelExtractionState();
            var characterCount = 0;

            foreach (var sheet in workbook.Descendants(MicrosoftExcelPackageFormat.SpreadsheetNamespace + "sheet"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = (string?)sheet.Attribute("state");
                if (state is "hidden" or "veryHidden")
                {
                    warnings.Add(MicrosoftExcelExtractionWarning.HiddenSheetIgnored);
                    continue;
                }
                if (++sheetCount > limits.MaximumSheets) return Result(MicrosoftExcelExtractionStatus.TooLarge, warnings);
                if (!sheetTargets.TryGetValue(
                        (string)sheet.Attribute(MicrosoftExcelPackageFormat.OfficeRelationshipsNamespace + "id")!,
                        out var target))
                    return Result(MicrosoftExcelExtractionStatus.CorruptedWorkbook, warnings);
                var worksheet = FindEntry(package, target);
                if (worksheet is null) return Result(MicrosoftExcelExtractionStatus.CorruptedWorkbook, warnings);
                var rows = await worksheetReader.ReadRowsAsync(worksheet, sharedStrings, styles, limits.MaximumCells, extractionState, warnings, cancellationToken);
                if (rows.Count == 0) continue;
                var sheetName = (string?)sheet.Attribute("name") ?? string.Empty;
                AddUnit(units, $"Feuille : {sheetName}", target, limits.MaximumExtractedCharacters, ref characterCount);
                var headerRowIndex = FindHeaderRowIndex(rows);
                var headers = headerRowIndex is { } index
                    ? rows[index].Cells.ToDictionary(cell => cell.ColumnIndex, cell => cell.Value)
                    : new Dictionary<int, string>();
                foreach (var row in rows)
                {
                    var values = row.Cells.Select(cell =>
                        headers.TryGetValue(cell.ColumnIndex, out var header)
                            && !string.IsNullOrWhiteSpace(header)
                            && !string.Equals(header, cell.Value, StringComparison.Ordinal)
                                ? $"{header} : {cell.Value}"
                                : $"{cell.Reference} : {cell.Value}");
                    AddUnit(units, string.Join(" | ", values), $"{target}!{row.Cells[0].Reference}:{row.Cells[^1].Reference}", limits.MaximumExtractedCharacters, ref characterCount);
                }
            }
            return units.Count == 0
                ? new MicrosoftExcelExtractionResult(MicrosoftExcelExtractionStatus.EmptyWorkbook, [], warnings.OrderBy(w => w).ToArray())
                : new MicrosoftExcelExtractionResult(MicrosoftExcelExtractionStatus.Success, units, warnings.OrderBy(w => w).ToArray());
        }
        catch (MicrosoftExcelExtractionLimitExceededException) { return Result(MicrosoftExcelExtractionStatus.TooLarge); }
        catch (InvalidDataException) { return Result(MicrosoftExcelExtractionStatus.CorruptedWorkbook); }
        catch (XmlException) { return Result(MicrosoftExcelExtractionStatus.CorruptedWorkbook); }
    }

    private static HashSet<MicrosoftExcelExtractionWarning> DetectWarnings(ZipArchive package)
    {
        var warnings = new HashSet<MicrosoftExcelExtractionWarning>();
        if (package.Entries.Any(e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))) warnings.Add(MicrosoftExcelExtractionWarning.MacroIgnored);
        if (package.Entries.Any(e => e.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase))) warnings.Add(MicrosoftExcelExtractionWarning.ExternalLinkIgnored);
        return warnings;
    }

    private static int? FindHeaderRowIndex(
        IReadOnlyList<MicrosoftExcelWorksheetRow> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Cells.Count >= 2)
            {
                return index;
            }
        }

        return null;
    }

    private static async Task<MemoryStream?> CopyWithinLimitAsync(Stream source, long max, CancellationToken token) { var destination = new MemoryStream(); var buffer = new byte[81920]; long total = 0; while (true) { var read = await source.ReadAsync(buffer, token); if (read == 0) { destination.Position = 0; return destination; } total += read; if (total > max) { await destination.DisposeAsync(); return null; } await destination.WriteAsync(buffer.AsMemory(0, read), token); } }
    private static void AddUnit(List<MicrosoftExcelExtractedUnit> units, string text, string source, int max, ref int count) { if (text.Length == 0) return; count = checked(count + text.Length); if (count > max) throw new MicrosoftExcelExtractionLimitExceededException(); units.Add(new(units.Count, text, source)); }
    private static bool HasSignature(Stream content, byte[] signature) { content.Position = 0; Span<byte> actual = stackalloc byte[signature.Length]; var read = content.Read(actual); content.Position = 0; return read == signature.Length && actual.SequenceEqual(signature); }
    private static bool HasZipSignature(Stream content) { content.Position = 0; Span<byte> signature = stackalloc byte[4]; var read = content.Read(signature); content.Position = 0; return read == 4 && signature[0] == 0x50 && signature[1] == 0x4B; }
    private static bool ExceedsExpandedSizeLimit(IReadOnlyCollection<ZipArchiveEntry> entries, long max) { long total = 0; foreach (var entry in entries) { if (entry.Length > max - total) return true; total += entry.Length; } return false; }
    private static ZipArchiveEntry? FindEntry(ZipArchive package, string path) => package.Entries.FirstOrDefault(e => e.FullName.Equals(path.TrimStart('/'), StringComparison.OrdinalIgnoreCase));
    private static string ResolvePath(string basePath, string target) { var path = target.StartsWith('/') ? target.TrimStart('/') : $"{basePath}/{target}"; var parts = new List<string>(); foreach (var part in path.Split('/')) { if (part == ".." && parts.Count > 0) parts.RemoveAt(parts.Count - 1); else if (part != ".") parts.Add(part); } return string.Join('/', parts); }
    private static MicrosoftExcelExtractionResult Result(MicrosoftExcelExtractionStatus status, IEnumerable<MicrosoftExcelExtractionWarning>? warnings = null) => new(status, [], (warnings ?? []).OrderBy(w => w).ToArray());
}

using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

internal sealed class MicrosoftExcelWorksheetReader
{
    private static readonly XNamespace Main = MicrosoftExcelPackageFormat.SpreadsheetNamespace;

    public async Task<IReadOnlyList<MicrosoftExcelWorksheetRow>> ReadRowsAsync(
        ZipArchiveEntry entry,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyles,
        int maximumCells,
        MicrosoftExcelExtractionState state,
        ISet<MicrosoftExcelExtractionWarning> warnings,
        CancellationToken cancellationToken)
    {
        var root = await LoadXmlAsync(entry, cancellationToken);
        var rows = new List<MicrosoftExcelWorksheetRow>();
        foreach (var row in root.Descendants(Main + "row"))
        {
            var cells = new List<MicrosoftExcelWorksheetCell>();
            foreach (var cell in row.Elements(Main + "c"))
            {
                var value = ReadCellValue(cell, sharedStrings, dateStyles, warnings);
                if (value.Length == 0)
                {
                    continue;
                }

                if (++state.CellCount > maximumCells)
                {
                    throw new MicrosoftExcelExtractionLimitExceededException();
                }

                var reference = (string?)cell.Attribute("r")
                    ?? throw new XmlException("A worksheet cell reference is missing.");
                cells.Add(new MicrosoftExcelWorksheetCell(
                    reference,
                    GetColumnIndex(reference),
                    value));
            }

            if (cells.Count > 0)
            {
                rows.Add(new MicrosoftExcelWorksheetRow(cells));
            }
        }

        return rows;
    }

    public async Task<XElement> LoadXmlAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 100_000_000
        });
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        return document.Root ?? throw new XmlException("Missing XML root.");
    }

    public async Task<IReadOnlyList<string>> ReadSharedStringsAsync(
        ZipArchiveEntry? entry,
        CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return [];
        }

        var root = await LoadXmlAsync(entry, cancellationToken);
        return root.Elements(Main + "si")
            .Select(si => string.Concat(si.Descendants(Main + "t").Select(text => text.Value)))
            .ToArray();
    }

    public async Task<IReadOnlySet<int>> ReadDateStylesAsync(
        ZipArchiveEntry? entry,
        CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return new HashSet<int>();
        }

        var root = await LoadXmlAsync(entry, cancellationToken);
        var dateFormats = root.Descendants(Main + "numFmt")
            .Where(element =>
            {
                var format = (string?)element.Attribute("formatCode");
                return format?.Contains("yy", StringComparison.OrdinalIgnoreCase) == true
                    || format?.Contains("dd", StringComparison.OrdinalIgnoreCase) == true;
            })
            .Select(element => (int)element.Attribute("numFmtId")!)
            .ToHashSet();
        var styles = root.Descendants(Main + "cellXfs")
            .Elements(Main + "xf")
            .Select((element, index) => new { index, id = (int?)element.Attribute("numFmtId") ?? 0 });

        return styles
            .Where(style => dateFormats.Contains(style.id) || style.id is >= 14 and <= 22)
            .Select(style => style.index)
            .ToHashSet();
    }

    private static string ReadCellValue(
        XElement cell,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyles,
        ISet<MicrosoftExcelExtractionWarning> warnings)
    {
        var type = (string?)cell.Attribute("t");
        var value = type == "inlineStr"
            ? string.Concat(cell.Descendants(Main + "t").Select(text => text.Value))
            : (string?)cell.Element(Main + "v");

        var formula = (string?)cell.Element(Main + "f");
        if (formula is not null && string.IsNullOrWhiteSpace(value))
        {
            warnings.Add(MicrosoftExcelExtractionWarning.FormulaValueUnavailable);
            return $"Formule sans résultat calculé : ={formula}";
        }

        if (value is null)
        {
            return string.Empty;
        }

        if (type == "s"
            && int.TryParse(value, out var sharedIndex)
            && sharedIndex >= 0
            && sharedIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedIndex];
        }

        if (type == "b")
        {
            return value == "1" ? "TRUE" : "FALSE";
        }

        if (type == "e")
        {
            return value;
        }

        if (dateStyles.Contains((int?)cell.Attribute("s") ?? -1)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            return DateTime.FromOADate(serial).ToString("O", CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static int GetColumnIndex(string cellReference)
    {
        var columnIndex = 0;
        var letterCount = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsAsciiLetter(character))
            {
                break;
            }

            columnIndex = checked(
                columnIndex * 26
                + char.ToUpperInvariant(character) - 'A'
                + 1);
            letterCount++;
        }

        if (letterCount == 0 || columnIndex == 0)
        {
            throw new XmlException("A worksheet cell reference is invalid.");
        }

        return columnIndex;
    }
}

internal sealed class MicrosoftExcelExtractionState
{
    public int CellCount { get; set; }
}

internal sealed record MicrosoftExcelWorksheetRow(IReadOnlyList<MicrosoftExcelWorksheetCell> Cells);

internal sealed record MicrosoftExcelWorksheetCell(
    string Reference,
    int ColumnIndex,
    string Value);

internal sealed class MicrosoftExcelExtractionLimitExceededException : Exception;

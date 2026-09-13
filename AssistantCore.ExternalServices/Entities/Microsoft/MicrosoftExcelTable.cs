namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftExcelTableWorkbook(
    IReadOnlyCollection<MicrosoftExcelTableWorksheet> Worksheets);

public sealed record MicrosoftExcelTableWorksheet(
    string Name,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

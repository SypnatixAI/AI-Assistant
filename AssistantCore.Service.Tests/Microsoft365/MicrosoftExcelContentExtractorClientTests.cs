using System.IO.Compression;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftExcelContentExtractorClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_MultipleSheetsAndCachedFormula_When_ExtractAsync_Then_ReturnsTraceableStructuredRows(string fileName)
    {
        // Given
        await using var package = CreateWorkbook(
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Budget\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Prévisions\" sheetId=\"2\" r:id=\"rId2\"/></sheets></workbook>",
            new Dictionary<string, string>
            {
                ["xl/worksheets/sheet1.xml"] = Worksheet("Budget", "Département", "Finance", "Montant", "125000", "B2", "SUM(B2:B10)"),
                ["xl/worksheets/sheet2.xml"] = Worksheet("Prévisions", "Trimestre", "T1", "Prévision", "132000", "B2", null),
                ["xl/_rels/workbook.xml.rels"] = Relationships("worksheets/sheet1.xml", "worksheets/sheet2.xml")
            });
        var client = new MicrosoftExcelContentExtractorClient();

        // When
        var result = await client.ExtractAsync($"{fileName}.xlsx", null, package, null, Limits, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftExcelExtractionStatus.Success, result.Status);
        Assert.Contains(result.Units, unit => unit.Text == "Département : Finance | Montant : 125000");
        Assert.Contains(result.Units, unit => unit.Text == "Trimestre : T1 | Prévision : 132000");
        Assert.Contains(result.Units, unit =>
            unit.Text == "Département : Finance | Montant : 125000"
            && unit.SourcePart.EndsWith("sheet1.xml!A2:B2", StringComparison.Ordinal));
    }

    [Theory, AutoDomainData]
    public async Task Given_MacroHiddenSheetAndUncachedFormula_When_ExtractAsync_Then_IgnoresActiveContentAndReportsWarnings(string fileName)
    {
        // Given
        await using var package = CreateWorkbook(
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Visible\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Hidden\" sheetId=\"2\" state=\"hidden\" r:id=\"rId2\"/></sheets></workbook>",
            new Dictionary<string, string>
            {
                ["xl/worksheets/sheet1.xml"] = "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\"><f>NOW()</f></c></row></sheetData></worksheet>",
                ["xl/worksheets/sheet2.xml"] = Worksheet("Hidden", "A", "B", "C", "D", "B2", null),
                ["xl/_rels/workbook.xml.rels"] = Relationships("worksheets/sheet1.xml", "worksheets/sheet2.xml"),
                ["xl/vbaProject.bin"] = "not executed"
            });
        var client = new MicrosoftExcelContentExtractorClient();

        // When
        var result = await client.ExtractAsync($"{fileName}.xlsm", null, package, null, Limits, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftExcelExtractionStatus.Success, result.Status);
        Assert.Contains(result.Units, unit =>
            unit.Text.Contains("Formule sans résultat calculé : =NOW()", StringComparison.Ordinal));
        Assert.Contains(MicrosoftExcelExtractionWarning.MacroIgnored, result.Warnings);
        Assert.Contains(MicrosoftExcelExtractionWarning.HiddenSheetIgnored, result.Warnings);
        Assert.Contains(MicrosoftExcelExtractionWarning.FormulaValueUnavailable, result.Warnings);
    }

    [Theory, AutoDomainData]
    public async Task Given_ASparseFinancialTable_When_ExtractAsync_Then_PreservesColumnAlignment(
        string fileName)
    {
        // Given
        await using var package = CreateWorkbook(
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Résultats 2026\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>",
            new Dictionary<string, string>
            {
                ["xl/worksheets/sheet1.xml"] =
                    "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"
                    + "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Rapport financier</t></is></c></row>"
                    + "<row r=\"2\"><c r=\"B2\" t=\"inlineStr\"><is><t>Janvier</t></is></c><c r=\"C2\" t=\"inlineStr\"><is><t>Février</t></is></c><c r=\"D2\" t=\"inlineStr\"><is><t>Mars</t></is></c></row>"
                    + "<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>Intérêts payés</t></is></c><c r=\"B3\"><v>100</v></c><c r=\"C3\"><v>200</v></c><c r=\"D3\"><v>300</v></c></row>"
                    + "</sheetData></worksheet>",
                ["xl/_rels/workbook.xml.rels"] =
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"/></Relationships>"
            });
        var client = new MicrosoftExcelContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xlsx",
            null,
            package,
            null,
            Limits,
            CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftExcelExtractionStatus.Success, result.Status);
        Assert.Contains(result.Units, unit => unit.Text == "A1 : Rapport financier");
        Assert.Contains(result.Units, unit =>
            unit.Text == "A3 : Intérêts payés | Janvier : 100 | Février : 200 | Mars : 300");
    }

    [Theory, AutoDomainData]
    public async Task Given_AFinancialTable_When_ReadAsync_Then_ReturnsEveryStructuredRow(
        string fileName)
    {
        // Given
        await using var package = CreateWorkbook(
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Transactions\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>",
            new Dictionary<string, string>
            {
                ["xl/worksheets/sheet1.xml"] =
                    "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"
                    + "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Transaction ID</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Transaction Amount</t></is></c></row>"
                    + "<row r=\"2\"><c r=\"A2\"><v>159</v></c><c r=\"B2\"><v>3100.50</v></c></row>"
                    + "<row r=\"3\"><c r=\"A3\"><v>308</v></c><c r=\"B3\"><v>4200.75</v></c></row>"
                    + "</sheetData></worksheet>",
                ["xl/_rels/workbook.xml.rels"] =
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"/></Relationships>"
            });
        var content = package.ToArray();
        var client = new MicrosoftExcelTableReaderClient();

        // When
        var workbook = await client.ReadAsync(
            $"{fileName}.xlsx",
            content,
            2_000_000,
            10,
            100,
            CancellationToken.None);

        // Then
        var worksheet = Assert.Single(workbook.Worksheets);
        Assert.Equal(2, worksheet.Rows.Count);
        Assert.Equal("3100.50", worksheet.Rows[0]["Transaction Amount"]);
        Assert.Equal("308", worksheet.Rows[1]["Transaction ID"]);
    }

    private static MicrosoftExcelExtractionLimits Limits => new(1_000_000, 2_000_000, 100_000, 10, 100);

    private static MemoryStream CreateWorkbook(string workbook, Dictionary<string, string> entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "xl/workbook.xml", workbook);
            foreach (var entry in entries) AddEntry(archive, entry.Key, entry.Value);
        }
        stream.Position = 0;
        return stream;
    }

    private static string Worksheet(string sheet, string header1, string value1, string header2, string value2, string reference, string? formula) =>
        $"<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>{header1}</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>{header2}</t></is></c></row><row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>{value1}</t></is></c><c r=\"{reference}\">{(formula is null ? "" : $"<f>{formula}</f>")}<v>{value2}</v></c></row></sheetData></worksheet>";

    private static string Relationships(string first, string second) =>
        $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Target=\"{first}\"/><Relationship Id=\"rId2\" Target=\"{second}\"/></Relationships>";

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }
}

using System.Xml.Linq;

namespace AssistantCore.ExternalServices.Services.Microsoft;

internal static class MicrosoftExcelPackageFormat
{
    public const string WorkbookPath = "xl/workbook.xml";
    public const string WorkbookRelationshipsPath = "xl/_rels/workbook.xml.rels";
    public const string SharedStringsPath = "xl/sharedStrings.xml";
    public const string StylesPath = "xl/styles.xml";
    public static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static readonly XNamespace OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly XNamespace PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".xlsm"
    };

    public static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/octet-stream",
        "application/zip",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel.sheet.macroEnabled.12"
    };
}

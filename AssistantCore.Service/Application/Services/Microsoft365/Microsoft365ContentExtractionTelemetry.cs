using System.Diagnostics.Metrics;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;

namespace AssistantCore.Service.Application.Services.Microsoft365;

internal static class Microsoft365ContentExtractionTelemetry
{
    private static readonly Meter Meter = new("AssistantCore.Microsoft365.Extraction");

    private static readonly Counter<long> ExtractionsByStatus =
        Meter.CreateCounter<long>("microsoft365.extraction.count");

    public static void RecordStatus(Microsoft365ContentExtractionStatus status) =>
        ExtractionsByStatus.Add(1, new KeyValuePair<string, object?>("microsoft365.extraction.status", status.ToString()));
}

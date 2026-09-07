using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public static class RagTelemetry
{
    public static readonly ActivitySource Activities = new("AssistantCore.Rag.Corrective");
    private static readonly Meter Meter = new("AssistantCore.Rag.Corrective");
    private static readonly Histogram<double> Values = Meter.CreateHistogram<double>("rag.stage.value");
    public static void Record(string name, double value)
    {
        Activity.Current?.SetTag(name, value);
        Values.Record(value, new KeyValuePair<string, object?>("rag.measurement", name));
    }
}

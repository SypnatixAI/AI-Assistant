using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AssistantCore.Service.Application.Commands.SendMessage;

internal static class MessageTelemetry
{
    public static readonly ActivitySource Activities = new("AssistantCore.Messages");

    private static readonly Meter Meter = new("AssistantCore.Messages");

    private static readonly Histogram<double> Durations =
        Meter.CreateHistogram<double>("messages.stage.duration_ms", unit: "ms");

    public static void RecordDuration(string name, double value) =>
        Durations.Record(value, new KeyValuePair<string, object?>("messages.measurement", name));
}

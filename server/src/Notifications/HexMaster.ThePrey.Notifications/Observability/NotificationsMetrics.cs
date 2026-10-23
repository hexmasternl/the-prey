using System.Diagnostics.Metrics;

namespace HexMaster.ThePrey.Notifications.Observability;

public sealed class NotificationsMetrics : INotificationsMetrics
{
    internal const string MeterName = "HexMaster.ThePrey.Notifications";

    private readonly Counter<long> _eventsForwarded;
    private readonly Counter<long> _eventsForwardFailed;
    private readonly Histogram<double> _forwardDuration;

    public NotificationsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _eventsForwarded = meter.CreateCounter<long>(
            "notifications.events_forwarded",
            unit: "{event}",
            description: "Total real-time events forwarded to Web PubSub groups");

        _eventsForwardFailed = meter.CreateCounter<long>(
            "notifications.events_forward_failed",
            unit: "{event}",
            description: "Total real-time events that failed to forward to Web PubSub");

        _forwardDuration = meter.CreateHistogram<double>(
            "notifications.forward.duration",
            unit: "ms",
            description: "Latency of forwarding an event to a Web PubSub group");
    }

    public void RecordEventForwarded(string eventType, double durationMs)
    {
        var tag = new KeyValuePair<string, object?>("event.type", eventType);
        _eventsForwarded.Add(1, tag);
        _forwardDuration.Record(durationMs, tag);
    }

    public void RecordEventForwardFailed(string eventType) =>
        _eventsForwardFailed.Add(1, new KeyValuePair<string, object?>("event.type", eventType));
}

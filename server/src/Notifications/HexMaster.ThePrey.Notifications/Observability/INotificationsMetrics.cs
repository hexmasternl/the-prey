namespace HexMaster.ThePrey.Notifications.Observability;

/// <summary>
/// Metrics for the Notifications module. Dimensions are low-cardinality only (event type, outcome) —
/// never game or user ids (those belong on trace spans, not aggregated metrics).
/// </summary>
public interface INotificationsMetrics
{
    /// <summary>Records a real-time event successfully forwarded to a Web PubSub group, with its latency.</summary>
    void RecordEventForwarded(string eventType, double durationMs);

    /// <summary>Records a failure while forwarding an event to Web PubSub.</summary>
    void RecordEventForwardFailed(string eventType);
}

using System.Diagnostics;

namespace HexMaster.ThePrey.Notifications.Observability;

internal static class NotificationsActivitySource
{
    internal const string SourceName = "HexMaster.ThePrey.Notifications";

    internal static readonly ActivitySource Source = new(SourceName);
}

/// <summary>Exposes the OpenTelemetry source/meter names for registration in the host project.</summary>
public static class NotificationsObservabilityConstants
{
    public const string ActivitySourceName = NotificationsActivitySource.SourceName;
    public const string MeterName = NotificationsMetrics.MeterName;
}

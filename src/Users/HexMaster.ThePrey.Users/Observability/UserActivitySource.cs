using System.Diagnostics;

namespace HexMaster.ThePrey.Users.Observability;

internal static class UserActivitySource
{
    internal const string SourceName = "HexMaster.ThePrey.Users";

    internal static readonly ActivitySource Source = new(SourceName);
}

/// <summary>Exposes constants used for OpenTelemetry registration in the host project.</summary>
public static class UserObservabilityConstants
{
    public const string ActivitySourceName = UserActivitySource.SourceName;
    public const string MeterName = UserMetrics.MeterName;
}

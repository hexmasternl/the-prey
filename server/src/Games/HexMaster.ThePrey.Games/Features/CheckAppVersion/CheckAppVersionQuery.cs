namespace HexMaster.ThePrey.Games.Features.CheckAppVersion;

/// <summary>
/// Asks whether <paramref name="CurrentVersion"/> (the raw client version string) meets the
/// configured minimum supported app version.
/// </summary>
public sealed record CheckAppVersionQuery(string? CurrentVersion);

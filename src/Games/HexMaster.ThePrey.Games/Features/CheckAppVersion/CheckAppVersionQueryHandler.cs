using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.Configuration;

namespace HexMaster.ThePrey.Games.Features.CheckAppVersion;

public sealed class CheckAppVersionQueryHandler : IQueryHandler<CheckAppVersionQuery, AppVersionCheckResult>
{
    /// <summary>
    /// Azure App Configuration key holding the minimum supported app version (e.g. <c>1.2.0</c>).
    /// Read through <see cref="IConfiguration"/> so the existing App Configuration refresh picks up
    /// runtime changes without a redeploy. Absent/empty means the gate is dormant.
    /// </summary>
    public const string MinimumVersionConfigurationKey = "Games:MinimumAppVersion";

    private readonly IConfiguration _configuration;

    public CheckAppVersionQueryHandler(IConfiguration configuration) => _configuration = configuration;

    public Task<AppVersionCheckResult> Handle(CheckAppVersionQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = GameActivitySource.Source.StartActivity("CheckAppVersion");

        try
        {
            if (!AppVersion.TryParse(query.CurrentVersion, out var current))
            {
                // Malformed payload — surfaced as 400 by the endpoint, distinct from the 409 gate.
                throw new ArgumentException("The supplied app version is not a valid version.", nameof(query));
            }

            // No minimum configured → gate is dormant, every well-formed client is up to date.
            if (!AppVersion.TryParse(_configuration[MinimumVersionConfigurationKey], out var minimum))
            {
                activity?.SetTag("version.outcome", "up_to_date");
                return Task.FromResult(AppVersionCheckResult.UpToDate);
            }

            var outcome = current.CompareTo(minimum) < 0
                ? AppVersionCheckResult.UpdateRequired
                : AppVersionCheckResult.UpToDate;

            activity?.SetTag(
                "version.outcome",
                outcome == AppVersionCheckResult.UpdateRequired ? "update_required" : "up_to_date");

            return Task.FromResult(outcome);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}

using System.Diagnostics.Metrics;

namespace HexMaster.ThePrey.Users.Observability;

public class UserMetrics : IUserMetrics
{
    internal const string MeterName = "HexMaster.ThePrey.Users";

    private readonly Counter<long> _usersCreated;

    public UserMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _usersCreated = meter.CreateCounter<long>(
            "users.created",
            unit: "{user}",
            description: "Total number of user accounts created");
    }

    public virtual void RecordUserCreated() => _usersCreated.Add(1);
}

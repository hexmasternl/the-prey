namespace HexMaster.ThePrey.PlayFields.DomainModels;

/// <summary>
/// A named, closed-polygon play area owned by a player. The ordered <see cref="Points"/> define the
/// polygon edges: each point connects to the next and the last point connects back to the first.
/// </summary>
public sealed class PlayField
{
    /// <summary>The minimum number of points required to form a polygon.</summary>
    public const int MinimumPoints = 3;

    private readonly List<GpsCoordinate> _points = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string OwnerId { get; private set; } = string.Empty;
    public bool IsPublic { get; private set; }
    public IReadOnlyList<GpsCoordinate> Points => _points.AsReadOnly();

    private PlayField() { }

    /// <summary>Creates a new play field, enforcing all domain invariants.</summary>
    public static PlayField Create(string name, string ownerId, IReadOnlyList<GpsCoordinate> points, bool isPublic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < MinimumPoints)
            throw new ArgumentException($"A play field requires at least {MinimumPoints} points.", nameof(points));

        var playField = new PlayField
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId,
            IsPublic = isPublic
        };
        playField._points.AddRange(points);
        return playField;
    }

    /// <summary>
    /// Reconstructs a previously-persisted play field. Intended only for data adapters; it trusts the
    /// supplied identifier and bypasses creation-time identity assignment.
    /// </summary>
    public static PlayField Rehydrate(Guid id, string name, string ownerId, bool isPublic, IReadOnlyList<GpsCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var playField = new PlayField
        {
            Id = id,
            Name = name,
            OwnerId = ownerId,
            IsPublic = isPublic
        };
        playField._points.AddRange(points);
        return playField;
    }

    /// <summary>
    /// Determines whether the supplied coordinate lies inside the closed polygon, using the
    /// ray-casting (even-odd rule) algorithm. Longitude is treated as X and latitude as Y.
    /// </summary>
    public bool IsInPlayfield(GpsCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        var inside = false;
        var x = coordinate.Longitude;
        var y = coordinate.Latitude;

        for (int i = 0, j = _points.Count - 1; i < _points.Count; j = i++)
        {
            var xi = _points[i].Longitude;
            var yi = _points[i].Latitude;
            var xj = _points[j].Longitude;
            var yj = _points[j].Latitude;

            var intersects = (yi > y) != (yj > y)
                && x < ((xj - xi) * (y - yi) / (yj - yi)) + xi;

            if (intersects)
                inside = !inside;
        }

        return inside;
    }
}

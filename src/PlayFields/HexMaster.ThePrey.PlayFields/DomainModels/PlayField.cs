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
    public DateTimeOffset LastModifiedOn { get; private set; }
    public GpsCoordinate? CenterCoordinates { get; private set; }
    public IReadOnlyList<GpsCoordinate> Points => _points.AsReadOnly();

    private PlayField() { }

    private static GpsCoordinate? ComputeCentroid(IReadOnlyList<GpsCoordinate> points)
    {
        if (points.Count == 0) return null;
        var lat = points.Average(p => p.Latitude);
        var lon = points.Average(p => p.Longitude);
        return new GpsCoordinate(lat, lon);
    }

    /// <summary>Creates a new play field, enforcing all domain invariants.</summary>
    public static PlayField Create(
        string name,
        string ownerId,
        IReadOnlyList<GpsCoordinate> points,
        bool isPublic,
        Guid? id = null,
        DateTimeOffset? lastModifiedOn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < MinimumPoints)
            throw new ArgumentException($"A play field requires at least {MinimumPoints} points.", nameof(points));

        var playField = new PlayField
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId,
            IsPublic = isPublic,
            LastModifiedOn = lastModifiedOn ?? DateTimeOffset.UtcNow
        };
        playField._points.AddRange(points);
        playField.CenterCoordinates = ComputeCentroid(points);
        return playField;
    }

    /// <summary>
    /// Reconstructs a previously-persisted play field. Intended only for data adapters; it trusts the
    /// supplied identifier and bypasses creation-time identity assignment.
    /// </summary>
    public static PlayField Rehydrate(
        Guid id,
        string name,
        string ownerId,
        bool isPublic,
        IReadOnlyList<GpsCoordinate> points,
        DateTimeOffset lastModifiedOn,
        GpsCoordinate? centerCoordinates = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        var playField = new PlayField
        {
            Id = id,
            Name = name,
            OwnerId = ownerId,
            IsPublic = isPublic,
            LastModifiedOn = lastModifiedOn
        };
        playField._points.AddRange(points);
        playField.CenterCoordinates = centerCoordinates ?? ComputeCentroid(points);
        return playField;
    }

    /// <summary>Updates the play field, re-validates invariants, recomputes the centroid, and stamps the timestamp.</summary>
    public void Update(string name, bool isPublic, IReadOnlyList<GpsCoordinate> points, DateTimeOffset lastModifiedOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < MinimumPoints)
            throw new ArgumentException($"A play field requires at least {MinimumPoints} points.", nameof(points));

        Name = name;
        IsPublic = isPublic;
        LastModifiedOn = lastModifiedOn;
        _points.Clear();
        _points.AddRange(points);
        CenterCoordinates = ComputeCentroid(points);
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

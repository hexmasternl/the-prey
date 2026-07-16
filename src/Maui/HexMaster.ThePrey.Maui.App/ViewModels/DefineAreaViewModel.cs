using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Navigation;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Holds the area editor's rules — the ordered vertex collection, the single selected vertex, and
/// add/select/move/delete over plain <see cref="GpsCoordinate"/> data — independent of Mapsui or any
/// platform. The page translates map gestures into these calls and redraws from <see cref="Vertices"/>
/// on each <see cref="Changed"/>. Save returns the ordered polygon (only when <see cref="CanSave"/>);
/// Cancel returns nothing. Fully unit-testable.
/// </summary>
public sealed class DefineAreaViewModel : ObservableObject
{
    /// <summary>Maximum number of vertices a polygon may have. Taps beyond this are ignored.</summary>
    public const int MaxPoints = 100;

    /// <summary>Minimum vertices for a saveable polygon.</summary>
    public const int MinPoints = 3;

    private readonly IAreaEditorNavigator _navigator;
    private readonly List<GpsCoordinate> _vertices = [];
    private int? _selectedIndex;

    public DefineAreaViewModel(IAreaEditorNavigator navigator)
    {
        _navigator = navigator;

        SaveCommand = new RelayCommand(SaveAsync, () => CanSave);
        CancelCommand = new RelayCommand(CancelAsync);
        ClearCommand = new RelayCommand(() => { Clear(); return Task.CompletedTask; }, () => HasVertices);
    }

    /// <summary>Raised after any change to the vertex set or selection so the page can redraw.</summary>
    public event EventHandler? Changed;

    /// <summary>The ordered polygon vertices.</summary>
    public IReadOnlyList<GpsCoordinate> Vertices => _vertices;

    /// <summary>The index of the selected vertex, or <c>null</c> when nothing is selected.</summary>
    public int? SelectedIndex
    {
        get => _selectedIndex;
        private set
        {
            if (SetProperty(ref _selectedIndex, value))
                OnPropertyChanged(nameof(HasSelection));
        }
    }

    /// <summary>True when a vertex is selected (drives the Trash action's visibility).</summary>
    public bool HasSelection => _selectedIndex.HasValue;

    /// <summary>True when any vertex exists (drives the Clear action's visibility).</summary>
    public bool HasVertices => _vertices.Count > 0;

    /// <summary>True when at least <see cref="MinPoints"/> vertices exist (drives Save enablement).</summary>
    public bool CanSave => _vertices.Count >= MinPoints;

    /// <summary>
    /// The polygon centroid (mean of the vertices), or <c>null</c> when there are no vertices. Used by the
    /// page to centre the map on an existing polygon when re-editing, instead of the current location.
    /// </summary>
    public GpsCoordinate? Centroid => _vertices.Count == 0
        ? null
        : new GpsCoordinate(_vertices.Average(v => v.Latitude), _vertices.Average(v => v.Longitude));

    /// <summary>Returns the ordered polygon and closes the editor. No-op when not saveable.</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>Closes the editor returning nothing.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>Removes all vertices at once, clearing the selection and the polygon.</summary>
    public RelayCommand ClearCommand { get; }

    /// <summary>Pre-populates the editor from an incoming polygon (e.g. re-editing a defined area).</summary>
    public void Seed(IReadOnlyList<GpsCoordinate> points)
    {
        _vertices.Clear();
        _vertices.AddRange(points);
        SelectedIndex = null;
        RaiseChanged();
    }

    /// <summary>Adds a vertex at the tapped location. Ignored once <see cref="MaxPoints"/> is reached.</summary>
    public void AddVertex(double latitude, double longitude)
    {
        if (_vertices.Count >= MaxPoints)
            return;

        _vertices.Add(new GpsCoordinate(latitude, longitude));
        RaiseChanged();
    }

    /// <summary>Selects the vertex at <paramref name="index"/> (single selection). Out-of-range is ignored.</summary>
    public void SelectVertex(int index)
    {
        if (index < 0 || index >= _vertices.Count)
            return;

        SelectedIndex = index;
        RaiseChanged();
    }

    /// <summary>Clears the current selection.</summary>
    public void ClearSelection()
    {
        if (_selectedIndex is null)
            return;

        SelectedIndex = null;
        RaiseChanged();
    }

    /// <summary>Moves the selected vertex to a new location. No-op when nothing is selected.</summary>
    public void MoveSelected(double latitude, double longitude)
    {
        if (_selectedIndex is not int index)
            return;

        _vertices[index] = new GpsCoordinate(latitude, longitude);
        RaiseChanged();
    }

    /// <summary>Removes the selected vertex and clears the selection. No-op when nothing is selected.</summary>
    public void DeleteSelected()
    {
        if (_selectedIndex is not int index)
            return;

        _vertices.RemoveAt(index);
        SelectedIndex = null;
        RaiseChanged();
    }

    /// <summary>Removes every vertex and clears the selection (the Clear-all action). No-op when empty.</summary>
    public void Clear()
    {
        if (_vertices.Count == 0)
        {
            SelectedIndex = null;
            return;
        }

        _vertices.Clear();
        SelectedIndex = null;
        RaiseChanged();
    }

    private Task SaveAsync() =>
        CanSave ? _navigator.ReturnAreaAsync(_vertices.ToList()) : Task.CompletedTask;

    private Task CancelAsync() => _navigator.ReturnAreaAsync(null);

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasVertices));
        OnPropertyChanged(nameof(Centroid));
        SaveCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

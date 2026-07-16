using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// A selectable row in the playfield-selection modal: wraps a <see cref="PlayFieldSummary"/> (kept
/// intact for the return value), exposes its name and visibility for the <c>PUBLIC</c>/<c>PRIVATE</c>
/// badge (same mapping as <see cref="PlayFieldListItem"/>), and carries an observable
/// <see cref="IsSelected"/> so the template can highlight the chosen row.
/// </summary>
public sealed class SelectablePlayFieldItem : ObservableObject
{
    private bool _isSelected;

    public SelectablePlayFieldItem(PlayFieldSummary summary)
    {
        Summary = summary;
    }

    /// <summary>The wrapped summary, returned to the caller on confirm.</summary>
    public PlayFieldSummary Summary { get; }

    public Guid Id => Summary.Id;

    public string Name => Summary.Name;

    public bool IsPublic => Summary.IsPublic;

    public bool IsPrivate => !Summary.IsPublic;

    /// <summary>True while this row is the single selected row (drives the highlight).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Builds a selectable row from a summary — the single mapping point from client results.</summary>
    public static SelectablePlayFieldItem From(PlayFieldSummary summary) => new(summary);

    /// <summary>Builds selectable rows from a sequence of summaries.</summary>
    public static IEnumerable<SelectablePlayFieldItem> FromMany(IEnumerable<PlayFieldSummary> summaries) =>
        summaries.Select(From);
}

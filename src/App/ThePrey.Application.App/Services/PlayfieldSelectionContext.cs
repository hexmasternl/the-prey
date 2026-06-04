using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

/// <summary>
/// Singleton state carrying the result of the playfield select view back to its caller
/// (mirrors <see cref="PlayfieldEditingContext"/>). The select view calls <see cref="Reset"/> when it
/// opens so a stale selection from an earlier flow can never leak; the caller reads
/// <see cref="SelectedPlayfield"/> when <see cref="SelectionCompleted"/> is true and then resets.
/// </summary>
public sealed class PlayfieldSelectionContext
{
    /// <summary>The playfield chosen by the user, or null when no selection was confirmed.</summary>
    public Playfield? SelectedPlayfield { get; set; }

    /// <summary>True only when the user confirmed a selection with the Select button.</summary>
    public bool SelectionCompleted { get; set; }

    public void Reset()
    {
        SelectedPlayfield = null;
        SelectionCompleted = false;
    }
}

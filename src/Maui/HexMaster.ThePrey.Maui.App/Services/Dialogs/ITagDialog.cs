using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Dialogs;

/// <summary>
/// A testable modal-selection seam for the hunter's Tag flow. Presents the in-range prey candidates
/// (callsign + distance) and resolves with the <c>UserId</c> of the tapped candidate, or <c>null</c>
/// when the hunter dismisses the dialog without choosing. Keeps <c>GameHudViewModel</c> free of MAUI
/// UI types so the tag orchestration stays unit-testable. The MAUI modal lives in the implementation.
/// </summary>
public interface ITagDialog
{
    /// <summary>
    /// Shows the candidate list and returns the selected candidate's <c>UserId</c>, or <c>null</c> on
    /// cancel/dismiss. Callers only invoke this with a non-empty list.
    /// </summary>
    Task<Guid?> SelectCandidateAsync(IReadOnlyList<TagCandidate> candidates);
}

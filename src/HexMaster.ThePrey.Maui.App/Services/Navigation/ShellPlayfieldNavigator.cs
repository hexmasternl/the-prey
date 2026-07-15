using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Shell-backed implementation of the create/edit/area-editor navigation seams. A single singleton
/// carries the result hand-off for every direction: it seeds the editor and awaits its polygon via a
/// <see cref="TaskCompletionSource{TResult}"/> (shared by create and edit), and stashes the created or
/// updated playfield for the list to pick up as it re-appears. This is the design's "lightweight result
/// channel", not an ambient draft store — it holds only in-flight hand-off state, cleared as each result
/// is consumed. Only one flow is active at a time, so the created/updated slots never collide.
/// </summary>
public sealed class ShellPlayfieldNavigator : ICreatePlayfieldNavigator, IEditPlayfieldNavigator, IAreaEditorNavigator
{
    /// <summary>Shell route for the Create Playfield page.</summary>
    public const string CreatePlayfieldRoute = "create-playfield";

    /// <summary>Shell route for the Edit Playfield page.</summary>
    public const string EditPlayfieldRoute = "edit-playfield";

    /// <summary>Shell route for the area editor page.</summary>
    public const string DefineAreaRoute = "define-area";

    private IReadOnlyList<GpsCoordinate> _areaSeed = [];
    private TaskCompletionSource<IReadOnlyList<GpsCoordinate>?>? _areaCompletion;
    private PlayFieldSummary? _created;
    private PlayFieldSummary? _updated;

    // ---- ICreatePlayfieldNavigator ----

    public Task<IReadOnlyList<GpsCoordinate>?> DefineAreaAsync(IReadOnlyList<GpsCoordinate> current) =>
        OpenAreaEditorAsync(current);

    public async Task ReturnToListAsync(PlayFieldSummary? created)
    {
        _created = created;
        await Shell.Current.GoToAsync("..");
    }

    public PlayFieldSummary? ConsumeCreated()
    {
        var created = _created;
        _created = null;
        return created;
    }

    // ---- IEditPlayfieldNavigator ----

    public Task<IReadOnlyList<GpsCoordinate>?> EditAreaAsync(IReadOnlyList<GpsCoordinate> current) =>
        OpenAreaEditorAsync(current);

    public async Task ReturnToListWithUpdateAsync(PlayFieldSummary? updated)
    {
        _updated = updated;
        await Shell.Current.GoToAsync("..");
    }

    public PlayFieldSummary? ConsumeUpdated()
    {
        var updated = _updated;
        _updated = null;
        return updated;
    }

    // ---- IAreaEditorNavigator ----

    public IReadOnlyList<GpsCoordinate> Seed => _areaSeed;

    public async Task ReturnAreaAsync(IReadOnlyList<GpsCoordinate>? points)
    {
        var completion = _areaCompletion;
        _areaCompletion = null;
        await Shell.Current.GoToAsync("..");
        completion?.TrySetResult(points);
    }

    // Shared by create and edit: seed the editor and await its result via one completion source.
    private async Task<IReadOnlyList<GpsCoordinate>?> OpenAreaEditorAsync(IReadOnlyList<GpsCoordinate> current)
    {
        _areaSeed = current;
        var completion = new TaskCompletionSource<IReadOnlyList<GpsCoordinate>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _areaCompletion = completion;

        await Shell.Current.GoToAsync(DefineAreaRoute);
        return await completion.Task;
    }
}

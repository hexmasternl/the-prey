using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Localization;

namespace HexMaster.ThePrey.Maui.App.Pages;

/// <summary>A single tag-candidate row: callsign and a formatted distance, carrying the participant id.</summary>
public sealed record TagCandidateRow(Guid UserId, string Callsign, string Distance);

/// <summary>
/// Modal that lists the preys the hunter may tag. Resolves the supplied completion source with the
/// tapped candidate's <c>UserId</c> on selection, or <c>null</c> on cancel / dismissal — guaranteed to
/// complete exactly once so the awaiting <see cref="Services.Dialogs.ITagDialog"/> caller never hangs.
/// </summary>
public partial class TagCandidatesPage : ContentPage
{
    private readonly TaskCompletionSource<Guid?> _completion;
    private bool _completed;

    public TagCandidatesPage(
        IReadOnlyList<TagCandidate> candidates,
        ILocalizationService localization,
        TaskCompletionSource<Guid?> completion)
    {
        InitializeComponent();
        _completion = completion;
        CandidatesList.ItemsSource = candidates
            .Select(c => new TagCandidateRow(c.UserId, c.Callsign, FormatDistance(c.DistanceMeters, localization)))
            .ToList();
    }

    private async void OnCandidateSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TagCandidateRow row)
            return;

        await CompleteAsync(row.UserId);
    }

    private async void OnCancelClicked(object? sender, EventArgs e) => await CompleteAsync(null);

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // A system back / swipe-dismiss that bypassed the buttons still resolves the caller (as cancel).
        if (!_completed)
        {
            _completed = true;
            _completion.TrySetResult(null);
        }
    }

    private async Task CompleteAsync(Guid? result)
    {
        if (_completed)
            return;

        _completed = true;
        _completion.TrySetResult(result);
        await Navigation.PopModalAsync();
    }

    private static string FormatDistance(double meters, ILocalizationService localization) =>
        meters < 1000d
            ? string.Format(localization["Hud_Distance_Meters"], (int)Math.Round(meters))
            : string.Format(localization["Hud_Distance_Kilometers"], (meters / 1000d).ToString("0.0"));
}

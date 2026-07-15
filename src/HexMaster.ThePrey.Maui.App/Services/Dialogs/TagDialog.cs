using HexMaster.ThePrey.Maui.App.Pages;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Localization;

namespace HexMaster.ThePrey.Maui.App.Services.Dialogs;

/// <summary>
/// Default <see cref="ITagDialog"/> that presents the candidate list as a modal <see cref="TagCandidatesPage"/>.
/// Pushes the modal on the main thread and awaits the page's completion source, resolving with the
/// selected candidate's <c>UserId</c> or <c>null</c> on cancel. Returns <c>null</c> when no page is
/// available rather than throwing.
/// </summary>
public sealed class TagDialog : ITagDialog
{
    private readonly ILocalizationService _localization;

    public TagDialog(ILocalizationService localization) => _localization = localization;

    public Task<Guid?> SelectCandidateAsync(IReadOnlyList<TagCandidate> candidates) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var navigation = Shell.Current?.Navigation
                ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
            if (navigation is null)
                return (Guid?)null;

            var completion = new TaskCompletionSource<Guid?>();
            var modal = new TagCandidatesPage(candidates, _localization, completion);
            await navigation.PushModalAsync(modal);
            return await completion.Task;
        });
}

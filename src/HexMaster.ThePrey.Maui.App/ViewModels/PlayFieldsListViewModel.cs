using System.Collections.ObjectModel;
using System.Windows.Input;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Storage;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the playfields list page: the Private/Public tab selection, the local-first private list
/// (cached list shown immediately, then a background refresh that reconciles + persists), and the
/// 300 ms-debounced, three-character-minimum public search. Plain .NET (MAUI/HTTP/file concerns are
/// behind interfaces, time behind <see cref="TimeProvider"/>) so it is fully unit-testable, including
/// the debounce and supersede behaviour.
/// </summary>
public sealed class PlayFieldsListViewModel : ObservableObject
{
    /// <summary>Minimum trimmed query length before a public search is sent (mirrors the backend).</summary>
    public const int MinimumSearchLength = 3;

    /// <summary>Debounce window for the public search.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly IPlayFieldApiClient _playFieldApi;
    private readonly IPlayFieldCache _cache;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IConfirmationDialog _confirmationDialog;
    private readonly ILocalizationService _localization;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PlayFieldsListViewModel> _logger;

    private CancellationTokenSource? _searchCts;

    private PlayFieldsTab _selectedTab = PlayFieldsTab.Private;
    private string _searchQuery = string.Empty;
    private bool _isBusy;
    private bool _privateIsEmpty;
    private bool _privateHasError;
    private bool _deleteError;
    private bool _publicShowPrompt = true;
    private bool _publicNoResults;
    private bool _publicHasError;

    public PlayFieldsListViewModel(
        IPlayFieldApiClient playFieldApi,
        IPlayFieldCache cache,
        IAccessTokenProvider accessTokenProvider,
        IConfirmationDialog confirmationDialog,
        ILocalizationService localization,
        TimeProvider timeProvider,
        ILogger<PlayFieldsListViewModel> logger)
    {
        _playFieldApi = playFieldApi;
        _cache = cache;
        _accessTokenProvider = accessTokenProvider;
        _confirmationDialog = confirmationDialog;
        _localization = localization;
        _timeProvider = timeProvider;
        _logger = logger;

        SelectPrivateCommand = new RelayCommand(() => { SelectedTab = PlayFieldsTab.Private; return Task.CompletedTask; });
        SelectPublicCommand = new RelayCommand(() => { SelectedTab = PlayFieldsTab.Public; return Task.CompletedTask; });
        DeletePlayFieldCommand = new RelayCommand<PlayFieldListItem>(item => DeletePlayFieldAsync(item));
    }

    /// <summary>The user's own playfields (Private tab). Populated cache-first, then from the backend.</summary>
    public ObservableCollection<PlayFieldListItem> PrivatePlayFields { get; } = [];

    /// <summary>The matching public playfields (Public tab search results).</summary>
    public ObservableCollection<PlayFieldListItem> PublicPlayFields { get; } = [];

    /// <summary>Switches to the Private tab.</summary>
    public ICommand SelectPrivateCommand { get; }

    /// <summary>Switches to the Public tab.</summary>
    public ICommand SelectPublicCommand { get; }

    /// <summary>Deletes the passed Private-list playfield after a confirmation dialog (swipe-to-delete).</summary>
    public ICommand DeletePlayFieldCommand { get; }

    /// <summary>The active tab. Defaults to <see cref="PlayFieldsTab.Private"/>.</summary>
    public PlayFieldsTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsPrivateSelected));
                OnPropertyChanged(nameof(IsPublicSelected));
            }
        }
    }

    /// <summary>True when the Private tab is active (drives the tab header + content visibility).</summary>
    public bool IsPrivateSelected => SelectedTab == PlayFieldsTab.Private;

    /// <summary>True when the Public tab is active.</summary>
    public bool IsPublicSelected => SelectedTab == PlayFieldsTab.Public;

    /// <summary>The public-search text. Editing it schedules a debounced search.</summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                ScheduleSearch();
        }
    }

    /// <summary>True while a private load or public search request is in flight.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>True when the private list is empty after a successful refresh with nothing cached.</summary>
    public bool PrivateIsEmpty
    {
        get => _privateIsEmpty;
        private set => SetProperty(ref _privateIsEmpty, value);
    }

    /// <summary>True when the private refresh failed and there was nothing cached to fall back to.</summary>
    public bool PrivateHasError
    {
        get => _privateHasError;
        private set => SetProperty(ref _privateHasError, value);
    }

    /// <summary>
    /// True when the last delete attempt failed (no token, unauthorized, forbidden, or network/error):
    /// the page binds this to a non-blocking indication and the item is kept. Cleared at the start of
    /// the next delete attempt.
    /// </summary>
    public bool DeleteError
    {
        get => _deleteError;
        private set => SetProperty(ref _deleteError, value);
    }

    /// <summary>True when the query is too short (or empty): the idle/prompt state, no request sent.</summary>
    public bool PublicShowPrompt
    {
        get => _publicShowPrompt;
        private set => SetProperty(ref _publicShowPrompt, value);
    }

    /// <summary>True when a search succeeded but matched no public playfields.</summary>
    public bool PublicNoResults
    {
        get => _publicNoResults;
        private set => SetProperty(ref _publicNoResults, value);
    }

    /// <summary>True when a search could not be completed (no session / backend error).</summary>
    public bool PublicHasError
    {
        get => _publicHasError;
        private set => SetProperty(ref _publicHasError, value);
    }

    /// <summary>
    /// Loads the Private tab, local-first. Shows the cached list immediately (no network wait), then
    /// refreshes from the backend in the background: on success it replaces the list and overwrites the
    /// cache; on failure it keeps the cached list, surfacing an error only when nothing was cached. The
    /// busy indicator blocks only the first run (empty cache); otherwise it is a non-blocking refresh hint.
    /// </summary>
    public async Task LoadPrivateAsync(CancellationToken ct = default)
    {
        var cached = await _cache.LoadAsync(ct);
        var hadCache = cached.Count > 0;
        ReplacePrivate(cached);

        // A full (blocking) load only when there is nothing cached to show.
        IsBusy = !hadCache;
        PrivateHasError = false;
        PrivateIsEmpty = false;

        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
            {
                MarkPrivateRefreshFailed(hadCache);
                return;
            }

            var result = await _playFieldApi.GetMyPlayFieldsAsync(token, ct);
            switch (result.Outcome)
            {
                case MyPlayFieldsOutcome.Success:
                    ReplacePrivate(result.Items);
                    await _cache.SaveAsync(result.Items, ct);
                    PrivateHasError = false;
                    PrivateIsEmpty = result.Items.Count == 0;
                    break;

                case MyPlayFieldsOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    MarkPrivateRefreshFailed(hadCache);
                    break;

                default:
                    MarkPrivateRefreshFailed(hadCache);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh the private playfields.");
            MarkPrivateRefreshFailed(hadCache);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Appends a newly created playfield to the Private list without a full reload (the append-UX after a
    /// successful create). Clears the empty/error states so the new item is shown immediately.
    /// </summary>
    public void AppendPrivate(PlayFieldSummary summary)
    {
        PrivatePlayFields.Add(new PlayFieldListItem(summary));
        PrivateIsEmpty = false;
        PrivateHasError = false;
    }

    /// <summary>
    /// Replaces the matching Private-list item (by id) in place after a successful edit — preserving scroll
    /// position and avoiding a full reload. Falls back to appending when the item isn't present (e.g. the
    /// list hasn't loaded yet) so the edit is still reflected.
    /// </summary>
    public void ReplacePrivate(PlayFieldSummary summary)
    {
        for (var i = 0; i < PrivatePlayFields.Count; i++)
        {
            if (PrivatePlayFields[i].Id == summary.Id)
            {
                PrivatePlayFields[i] = new PlayFieldListItem(summary);
                PrivateHasError = false;
                return;
            }
        }

        AppendPrivate(summary);
    }

    /// <summary>
    /// The swipe-to-delete flow for one Private-list playfield: confirm → authenticate → delete →
    /// mutate the list. Removes the item only on <see cref="DeletePlayFieldOutcome.Success"/> or
    /// <see cref="DeletePlayFieldOutcome.NotFound"/> (the playfield is gone either way); every other
    /// outcome keeps the item and raises <see cref="DeleteError"/>. Never removes optimistically.
    /// </summary>
    public async Task DeletePlayFieldAsync(PlayFieldListItem item, CancellationToken ct = default)
    {
        if (item is null)
            return;

        // Cleared at the start of every attempt so a stale error never lingers.
        DeleteError = false;

        var confirmed = await _confirmationDialog.ConfirmAsync(
            _localization["Playfields_Delete_ConfirmTitle"],
            _localization["Playfields_Delete_ConfirmMessage"],
            _localization["Playfields_Delete_ConfirmAccept"],
            _localization["Playfields_Delete_ConfirmCancel"]);
        if (!confirmed)
            return; // Cancel is a true no-op: no request, item stays.

        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
            {
                DeleteError = true; // No session to authenticate the delete — keep the item.
                return;
            }

            var result = await _playFieldApi.DeletePlayFieldAsync(item.Id, token, ct);
            switch (result.Outcome)
            {
                case DeletePlayFieldOutcome.Success:
                case DeletePlayFieldOutcome.NotFound:
                    RemovePrivate(item);
                    break;

                case DeletePlayFieldOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    DeleteError = true;
                    break;

                default: // Forbidden / Error — keep the item, surface the error.
                    DeleteError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete a private playfield.");
            DeleteError = true;
        }
    }

    // Removes the item from the private list by identity, converging the UI to the backend truth.
    private void RemovePrivate(PlayFieldListItem item)
    {
        PrivatePlayFields.Remove(item);
        PrivateIsEmpty = PrivatePlayFields.Count == 0;
    }

    // Keep a cached list on screen; only surface the error state when there was nothing cached.
    private void MarkPrivateRefreshFailed(bool hadCache)
    {
        PrivateIsEmpty = false;
        PrivateHasError = !hadCache;
    }

    private void ScheduleSearch()
    {
        CancelPendingSearch();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = DebouncedSearchAsync(_searchQuery, cts.Token);
    }

    private async Task DebouncedSearchAsync(string rawQuery, CancellationToken ct)
    {
        try
        {
            await Task.Delay(DebounceDelay, _timeProvider, ct);
        }
        catch (TaskCanceledException)
        {
            return; // Superseded by a newer keystroke.
        }

        var query = rawQuery?.Trim() ?? string.Empty;
        if (query.Length < MinimumSearchLength)
        {
            // Too short: send nothing and show the idle prompt.
            ReplacePublic([]);
            PublicShowPrompt = true;
            PublicNoResults = false;
            PublicHasError = false;
            IsBusy = false;
            return;
        }

        IsBusy = true;
        PublicShowPrompt = false;
        PublicNoResults = false;
        PublicHasError = false;

        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (ct.IsCancellationRequested)
                return; // Superseded — discard.

            if (token is null)
            {
                SetPublicError();
                return;
            }

            var result = await _playFieldApi.SearchPublicPlayFieldsAsync(query, token, ct);
            if (ct.IsCancellationRequested)
                return; // A newer query superseded this one — only the latest results are applied.

            switch (result.Outcome)
            {
                case PublicPlayFieldsOutcome.Success:
                    ReplacePublic(result.Items);
                    PublicNoResults = result.Items.Count == 0;
                    PublicShowPrompt = false;
                    PublicHasError = false;
                    break;

                case PublicPlayFieldsOutcome.ValidationTooShort:
                    ReplacePublic([]);
                    PublicShowPrompt = true;
                    break;

                case PublicPlayFieldsOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    SetPublicError();
                    break;

                default:
                    SetPublicError();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded — leave state for the newer search to update.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Public-playfields search failed.");
            SetPublicError();
        }
        finally
        {
            // Only the current (non-superseded) search clears the busy indicator.
            if (!ct.IsCancellationRequested)
                IsBusy = false;
        }
    }

    private void SetPublicError()
    {
        ReplacePublic([]);
        PublicShowPrompt = false;
        PublicNoResults = false;
        PublicHasError = true;
    }

    private void ReplacePrivate(IReadOnlyList<PlayFieldSummary> items) => Replace(PrivatePlayFields, items);

    private void ReplacePublic(IReadOnlyList<PlayFieldSummary> items) => Replace(PublicPlayFields, items);

    private static void Replace(ObservableCollection<PlayFieldListItem> target, IReadOnlyList<PlayFieldSummary> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(new PlayFieldListItem(item));
    }

    private void CancelPendingSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }
}

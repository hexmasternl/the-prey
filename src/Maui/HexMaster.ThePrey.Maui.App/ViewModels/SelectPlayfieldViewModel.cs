using System.Collections.ObjectModel;
using System.Windows.Input;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Storage;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the playfield-selection modal: the cache-first own-playfields default list, the
/// 3-character-minimum, 300 ms-debounced search that merges the user's own (private + public)
/// playfields with matching public ones (de-duplicated by id, own winning), single-row selection with a
/// <c>SELECT</c> confirm, and the confirm/cancel hand-off through the navigator's result sink. Plain .NET
/// (HTTP, cache, and time behind interfaces / <see cref="TimeProvider"/>) so it is fully unit-testable,
/// including the debounce and supersede behaviour.
/// </summary>
public sealed class SelectPlayfieldViewModel : ObservableObject
{
    /// <summary>Minimum trimmed query length before a search is sent (mirrors the backend + the list page).</summary>
    public const int MinimumSearchLength = 3;

    /// <summary>Debounce window for the search.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly IPlayFieldApiClient _playFieldApi;
    private readonly IPlayFieldCache _cache;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IPlayfieldSelectResultSink _resultSink;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SelectPlayfieldViewModel> _logger;

    private CancellationTokenSource? _searchCts;
    private List<PlayFieldSummary> _ownItems = [];
    private bool _isSearching;

    private string _searchQuery = string.Empty;
    private SelectablePlayFieldItem? _selectedItem;
    private bool _isBusy;
    private bool _hasError;

    public SelectPlayfieldViewModel(
        IPlayFieldApiClient playFieldApi,
        IPlayFieldCache cache,
        IAccessTokenProvider accessTokenProvider,
        IPlayfieldSelectResultSink resultSink,
        TimeProvider timeProvider,
        ILogger<SelectPlayfieldViewModel> logger)
    {
        _playFieldApi = playFieldApi;
        _cache = cache;
        _accessTokenProvider = accessTokenProvider;
        _resultSink = resultSink;
        _timeProvider = timeProvider;
        _logger = logger;

        SelectCommand = new RelayCommand<SelectablePlayFieldItem>(item => { ToggleSelect(item); return Task.CompletedTask; });
        ConfirmCommand = new RelayCommand(ConfirmAsync, () => CanSelect);
        CancelCommand = new RelayCommand(CancelAsync);
    }

    /// <summary>The rows shown — either the default own list or the merged search results.</summary>
    public ObservableCollection<SelectablePlayFieldItem> Items { get; } = [];

    /// <summary>Selects / toggles a row.</summary>
    public ICommand SelectCommand { get; }

    /// <summary>Confirms the selection, returning it to the caller. Enabled only when a row is selected.</summary>
    public RelayCommand ConfirmCommand { get; }

    /// <summary>Cancels the modal, returning nothing to the caller.</summary>
    public RelayCommand CancelCommand { get; }

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

    /// <summary>The single selected row, or null when nothing is selected.</summary>
    public SelectablePlayFieldItem? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (SetProperty(ref _selectedItem, value))
                OnSelectionChanged();
        }
    }

    /// <summary>True while a load or search request is in flight (blocking only on the first empty-cache load).</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) UpdateRegions(); }
    }

    /// <summary>True when a load or search failed (and there was nothing to fall back to).</summary>
    public bool HasError
    {
        get => _hasError;
        private set { if (SetProperty(ref _hasError, value)) UpdateRegions(); }
    }

    /// <summary>The SELECT button is enabled only when a row is selected and nothing is in flight.</summary>
    public bool CanSelect => SelectedItem is not null && !IsBusy;

    /// <summary>Default mode with no own playfields — shows the empty-state message.</summary>
    public bool IsEmpty => !IsBusy && !HasError && !_isSearching && Items.Count == 0;

    /// <summary>Search mode that matched nothing — shows the no-results message.</summary>
    public bool ShowNoResults => !IsBusy && !HasError && _isSearching && Items.Count == 0;

    /// <summary>
    /// Loads the default own-playfields list, local-first: shows the cached list immediately, then
    /// reconciles with <c>GET /playfields</c>. Success replaces the list and re-caches it; a failed
    /// refresh keeps the cached list (error only when nothing was cached); Unauthorized invalidates the
    /// token and shows the error state. The busy indicator blocks only when the cache was empty.
    /// </summary>
    public async Task LoadDefaultAsync(CancellationToken ct = default)
    {
        var cached = await _cache.LoadAsync(ct);
        var hadCache = cached.Count > 0;
        _ownItems = [.. cached];
        _isSearching = false;
        ReplaceItems(cached);

        IsBusy = !hadCache;
        HasError = false;
        UpdateRegions();

        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
            {
                MarkDefaultRefreshFailed(hadCache);
                return;
            }

            var result = await _playFieldApi.GetMyPlayFieldsAsync(token, ct);
            switch (result.Outcome)
            {
                case MyPlayFieldsOutcome.Success:
                    _ownItems = [.. result.Items];
                    await _cache.SaveAsync(result.Items, ct);
                    if (!_isSearching)
                        ReplaceItems(result.Items);
                    HasError = false;
                    break;

                case MyPlayFieldsOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    MarkDefaultRefreshFailed(hadCache);
                    break;

                default:
                    MarkDefaultRefreshFailed(hadCache);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh the own playfields for selection.");
            MarkDefaultRefreshFailed(hadCache);
        }
        finally
        {
            IsBusy = false;
            UpdateRegions();
        }
    }

    /// <summary>Selects the tapped row, clearing the previous one; re-tapping the selected row clears it.</summary>
    public void ToggleSelect(SelectablePlayFieldItem? item)
    {
        if (item is null)
            return;

        if (ReferenceEquals(item, SelectedItem))
        {
            item.IsSelected = false;
            SelectedItem = null;
            return;
        }

        if (SelectedItem is not null)
            SelectedItem.IsSelected = false;

        item.IsSelected = true;
        SelectedItem = item;
    }

    internal async Task ConfirmAsync()
    {
        if (SelectedItem is null)
            return;

        await _resultSink.CompleteAsync(SelectedItem.Summary);
    }

    internal Task CancelAsync() => _resultSink.CompleteAsync(null);

    private void MarkDefaultRefreshFailed(bool hadCache)
    {
        // Keep a cached list on screen; only surface the error when there was nothing cached.
        HasError = !hadCache;
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
            RestoreDefaultList();
            return;
        }

        IsBusy = true;
        _isSearching = true;
        HasError = false;
        UpdateRegions();

        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (ct.IsCancellationRequested)
                return;

            if (token is null)
            {
                SetSearchError();
                return;
            }

            // Own list filtered locally (case-insensitive contains) — the only way private matches appear.
            var ownMatches = _ownItems
                .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var publicResult = await _playFieldApi.SearchPublicPlayFieldsAsync(query, token, ct);
            if (ct.IsCancellationRequested)
                return; // A newer query superseded this one — only the latest results apply.

            switch (publicResult.Outcome)
            {
                case PublicPlayFieldsOutcome.Success:
                    // Merge own + public, de-duplicated by id with own entries winning.
                    var seen = new HashSet<Guid>(ownMatches.Select(o => o.Id));
                    var merged = ownMatches
                        .Concat(publicResult.Items.Where(p => seen.Add(p.Id)))
                        .ToList();
                    ReplaceItems(merged);
                    _isSearching = true;
                    HasError = false;
                    break;

                case PublicPlayFieldsOutcome.ValidationTooShort:
                    RestoreDefaultList();
                    break;

                case PublicPlayFieldsOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    SetSearchError();
                    break;

                default:
                    SetSearchError();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded — leave state for the newer search to update.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Playfield selection search failed.");
            SetSearchError();
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsBusy = false;
                UpdateRegions();
            }
        }
    }

    private void RestoreDefaultList()
    {
        _isSearching = false;
        HasError = false;
        ReplaceItems(_ownItems);
        IsBusy = false;
        UpdateRegions();
    }

    private void SetSearchError()
    {
        ReplaceItems([]);
        _isSearching = true;
        HasError = true;
        UpdateRegions();
    }

    // Replaces the displayed rows and clears any selection (the previous rows are gone).
    private void ReplaceItems(IReadOnlyList<PlayFieldSummary> items)
    {
        if (SelectedItem is not null)
        {
            SelectedItem.IsSelected = false;
            SelectedItem = null;
        }

        Items.Clear();
        foreach (var item in items)
            Items.Add(SelectablePlayFieldItem.From(item));

        UpdateRegions();
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(CanSelect));
        ConfirmCommand.RaiseCanExecuteChanged();
    }

    private void UpdateRegions()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowNoResults));
        OnPropertyChanged(nameof(CanSelect));
        ConfirmCommand.RaiseCanExecuteChanged();
    }

    private void CancelPendingSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }
}

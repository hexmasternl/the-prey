using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

/// <summary>A row on the playfield select view; visual state is precomputed for binding.</summary>
public sealed class PlayfieldSelectItem
{
    // Design-system tokens (mirror Colors.xaml).
    private static readonly Color SignalColor = Color.FromArgb("#64FF00");
    private static readonly Color SurfaceColor = Color.FromArgb("#23271F");
    private static readonly Color SurfacePlusColor = Color.FromArgb("#2D3328");
    private static readonly Color TextPrimaryColor = Color.FromArgb("#DCF6D2");
    private static readonly Color TextSoftColor = Color.FromArgb("#8C9A83");
    private static readonly Color TextGhostColor = Color.FromArgb("#5A6553");
    private static readonly Color HunterDimColor = Color.FromArgb("#A01408");

    public required Playfield Playfield { get; init; }
    public required bool IsSelectable { get; init; }
    public required bool IsSelected { get; init; }

    public string Name => Playfield.Name;

    /// <summary>Owner name for server results, or the not-synced hint for disabled rows.</summary>
    public string Subtitle => IsSelectable ? Playfield.OwnerName ?? string.Empty : AppLocalizer.NotSyncedHint;
    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

    public string Marker => IsSelected ? "✓" : string.Empty;
    public Color RowColor => IsSelected ? SurfacePlusColor : SurfaceColor;
    public Color NameColor => IsSelectable ? (IsSelected ? SignalColor : TextPrimaryColor) : TextGhostColor;
    public Color SubtitleColor => IsSelectable ? TextSoftColor : HunterDimColor;
}

public partial class PlayfieldSelectPage : ContentPage
{
    private const int MinimumSearchLength = 3;
    private const int SearchDebounceMilliseconds = 400;

    private readonly IPlayfieldService _playfieldService;
    private readonly PlayfieldCacheService _cache;
    private readonly PlayfieldSelectionContext _selectionContext;

    private List<Playfield> _localPlayfields = [];
    private List<Playfield> _currentList = [];
    private Playfield? _selectedPlayfield;
    private CancellationTokenSource? _searchCts;

    public PlayfieldSelectPage(
        IPlayfieldService playfieldService,
        PlayfieldCacheService cache,
        PlayfieldSelectionContext selectionContext)
    {
        InitializeComponent();
        _playfieldService = playfieldService;
        _cache = cache;
        _selectionContext = selectionContext;

        Title = AppLocalizer.PlayfieldSelectTitle;
        SearchEntry.Placeholder = AppLocalizer.SearchPlaceholder;
        SelectButton.Text = AppLocalizer.SelectButton;
    }

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // A fresh open never carries a stale selection from an earlier flow.
        _selectionContext.Reset();
        _selectedPlayfield = null;
        UpdateSelectButton();

        _localPlayfields = (await _cache.LoadAsync()).ToList();
        ShowDefaultList();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CancelSearch();
    }

    // ─── List rendering ──────────────────────────────────────────────────────

    /// <summary>The default (non-search) state: all locally cached playfields, unsynced ones disabled.</summary>
    private void ShowDefaultList() =>
        RenderList(_localPlayfields);

    private void RenderList(List<Playfield> playfields)
    {
        _currentList = playfields;

        // Drop a selection that is no longer in the rendered list.
        if (_selectedPlayfield is not null && playfields.All(p => p.Id != _selectedPlayfield.Id))
        {
            _selectedPlayfield = null;
            UpdateSelectButton();
        }

        PlayfieldsList.ItemsSource = playfields
            .Select(p => new PlayfieldSelectItem
            {
                Playfield = p,
                IsSelectable = p.IsSynchronized,
                IsSelected = p.Id == _selectedPlayfield?.Id,
            })
            .ToList();

        var isEmpty = playfields.Count == 0;
        PlayfieldsList.IsVisible = !isEmpty;
        EmptyStateLabel.Text = IsInSearchMode ? AppLocalizer.SearchEmpty : AppLocalizer.PlayfieldSelectEmpty;
        EmptyStateLabel.IsVisible = isEmpty;
    }

    private bool IsInSearchMode => (SearchEntry.Text?.Trim().Length ?? 0) >= MinimumSearchLength;

    // ─── Search ──────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e.NewTextValue?.Trim() ?? string.Empty;

        CancelSearch();

        if (text.Length < MinimumSearchLength)
        {
            SetLoading(false);
            ShowDefaultList();
            return;
        }

        _searchCts = new CancellationTokenSource();
        _ = ExecuteSearchAsync(text, _searchCts.Token);
    }

    private async Task ExecuteSearchAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(SearchDebounceMilliseconds, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Local private matches render regardless of how the server call goes.
        var localMatches = _localPlayfields
            .Where(p => p.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        SetLoading(true);

        var serverFailed = false;
        var serverResults = new List<Playfield>();
        try
        {
            serverResults = (await _playfieldService.SearchPublicPlayfieldsAsync(query, ct)).ToList();
        }
        catch (OperationCanceledException)
        {
            return; // Superseded by newer input — that search updates the UI.
        }
        catch
        {
            serverFailed = true;
        }

        if (ct.IsCancellationRequested)
            return;

        // Merge: de-duplicate by id, the local copy wins (it carries the sync state).
        var localIds = localMatches.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var merged = localMatches
            .Concat(serverResults.Where(p => !localIds.Contains(p.Id)).Select(MarkServerResultSynced))
            .ToList();

        SetLoading(false);
        RenderList(merged);

        if (serverFailed)
            _ = ShowToastAsync(AppLocalizer.SearchError);
    }

    /// <summary>Server search results are server-truth and therefore always selectable.</summary>
    private static Playfield MarkServerResultSynced(Playfield playfield)
    {
        playfield.IsSynchronized = true;
        return playfield;
    }

    private void SetLoading(bool loading)
    {
        SearchLoadingIndicator.IsRunning = loading;
        SearchLoadingIndicator.IsVisible = loading;
        if (loading)
        {
            PlayfieldsList.IsVisible = false;
            EmptyStateLabel.IsVisible = false;
        }
    }

    private void CancelSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }

    // ─── Selection ───────────────────────────────────────────────────────────

    private void OnPlayfieldTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not PlayfieldSelectItem item)
            return;

        if (!item.IsSelectable)
        {
            // Unsynced rows can never be selected; explain why.
            _ = ShowToastAsync(AppLocalizer.PlayfieldNotSyncedMessage);
            return;
        }

        _selectedPlayfield = item.Playfield;
        UpdateSelectButton();
        RenderList(_currentList);
    }

    private void UpdateSelectButton() =>
        SelectButton.IsEnabled = _selectedPlayfield is not null;

    private async void OnSelectClicked(object? sender, EventArgs e)
    {
        if (_selectedPlayfield is null)
            return;

        _selectionContext.SelectedPlayfield = _selectedPlayfield;
        _selectionContext.SelectionCompleted = true;
        await Shell.Current.GoToAsync("..");
    }

    // ─── Toast ───────────────────────────────────────────────────────────────

    private async Task ShowToastAsync(string message)
    {
        ToastLabel.Text = message;
        ToastBorder.Opacity = 0;
        ToastBorder.IsVisible = true;
        await ToastBorder.FadeToAsync(1, 200);
        await Task.Delay(3000);
        await ToastBorder.FadeToAsync(0, 400);
        ToastBorder.IsVisible = false;
    }
}

using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public partial class PlayfieldsPage : ContentPage
{
    private readonly IPlayfieldService _playfieldService;
    private readonly PlayfieldCacheService _cache;
    private readonly PlayfieldSyncService _sync;
    private List<Playfield> _playfields = [];
    private bool _isPublicTabActive;
    private CancellationTokenSource? _searchCts;

    public PlayfieldsPage(IPlayfieldService playfieldService, PlayfieldCacheService cache, PlayfieldSyncService sync)
    {
        InitializeComponent();
        _playfieldService = playfieldService;
        _cache = cache;
        _sync = sync;

        Title = AppLocalizer.PlayfieldsPageTitle;
        CreateNewButton.Text = AppLocalizer.PlayfieldsCreateNew;
        PrivateTabBtn.Text = AppLocalizer.TabPrivate;
        PublicTabBtn.Text = AppLocalizer.TabPublic;
        SearchEntry.Placeholder = AppLocalizer.SearchPlaceholder;
        SearchPromptLabel.Text = AppLocalizer.SearchPrompt;
        SearchEmptyLabel.Text = AppLocalizer.SearchEmpty;
        SearchErrorLabel.Text = AppLocalizer.SearchError;
    }

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_isPublicTabActive)
            await LoadPlayfieldsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CancelSearch();
    }

    // ─── Tab switching ───────────────────────────────────────────────────────

    private void OnPrivateTabClicked(object? sender, EventArgs e)
    {
        if (!_isPublicTabActive) return;
        _isPublicTabActive = false;
        CancelSearch();
        PrivateContent.IsVisible = true;
        PublicContent.IsVisible = false;
        SetTabButtonStyles();
    }

    private void OnPublicTabClicked(object? sender, EventArgs e)
    {
        if (_isPublicTabActive) return;
        _isPublicTabActive = true;
        PrivateContent.IsVisible = false;
        PublicContent.IsVisible = true;
        SetTabButtonStyles();
    }

    // Design system tokens used for tab active/inactive state
    private static readonly Color SignalColor = Color.FromArgb("#64FF00");
    private static readonly Color TextGhostColor = Color.FromArgb("#5A6553");
    private static readonly Color BaseColor = Color.FromArgb("#181B17");
    private static readonly Color SurfaceColor = Color.FromArgb("#23271F");

    private void SetTabButtonStyles()
    {
        PrivateTabBtn.TextColor = _isPublicTabActive ? TextGhostColor : SignalColor;
        PrivateTabBtn.BackgroundColor = _isPublicTabActive ? BaseColor : SurfaceColor;

        PublicTabBtn.TextColor = _isPublicTabActive ? SignalColor : TextGhostColor;
        PublicTabBtn.BackgroundColor = _isPublicTabActive ? SurfaceColor : BaseColor;
    }

    // ─── Private tab: load ───────────────────────────────────────────────────

    private async Task LoadPlayfieldsAsync()
    {
        SetPrivateState(loading: true);
        try
        {
            await _sync.SyncAsync();
        }
        catch (UnauthorizedException)
        {
            SetPrivateState(loading: false);
            await Shell.Current.GoToAsync(AppShell.LoginRoute);
            return;
        }
        catch
        {
            // Sync failure is non-fatal — fall through to display whatever is cached.
        }

        _playfields = (await _cache.LoadAsync()).ToList();
        if (_playfields.Count == 0)
        {
            ShowPrivateEmpty(AppLocalizer.PlayfieldsOfflineEmpty);
            return;
        }

        SetPrivateState(loading: false);
        PlayfieldsList.ItemsSource = _playfields;
        PlayfieldsList.IsVisible = true;
    }

    private void SetPrivateState(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        EmptyStateLabel.IsVisible = false;
        PlayfieldsList.IsVisible = false;
    }

    private void ShowPrivateEmpty(string message)
    {
        LoadingIndicator.IsRunning = false;
        LoadingIndicator.IsVisible = false;
        EmptyStateLabel.Text = message;
        EmptyStateLabel.IsVisible = true;
        PlayfieldsList.IsVisible = false;
    }

    // ─── Private tab: actions ────────────────────────────────────────────────

    private async void OnCreateNewClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppShell.PlayfieldDetailsRoute);

    private async void OnPlayfieldTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not Playfield playfield)
            return;
        await Shell.Current.GoToAsync($"{AppShell.PlayfieldDetailsRoute}?id={Uri.EscapeDataString(playfield.Id)}");
    }

    private async void OnDeleteSwipeInvoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not Playfield playfield)
            return;

        var confirmed = await DisplayAlertAsync(
            AppLocalizer.PlayfieldsDeleteTitle,
            string.Format(AppLocalizer.PlayfieldsDeleteMessage, playfield.Name),
            AppLocalizer.PlayfieldsDeleteConfirm,
            AppLocalizer.Cancel);

        if (!confirmed)
            return;

        try
        {
            await _playfieldService.DeletePlayfieldAsync(playfield.Id);
            _playfields.Remove(playfield);
            await _cache.SaveAsync(_playfields);
            PlayfieldsList.ItemsSource = null;
            PlayfieldsList.ItemsSource = _playfields;
        }
        catch (UnauthorizedException)
        {
            await Shell.Current.GoToAsync(AppShell.LoginRoute);
        }
        catch
        {
            await DisplayAlertAsync(AppLocalizer.Error, AppLocalizer.PlayfieldsDeleteError, AppLocalizer.Ok);
        }
    }

    // ─── Public tab: search ──────────────────────────────────────────────────

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e.NewTextValue ?? string.Empty;

        CancelSearch();

        if (text.Length < 3)
        {
            ShowPublicPrompt();
            return;
        }

        _searchCts = new CancellationTokenSource();
        _ = ExecuteSearchAsync(text, _searchCts.Token);
    }

    private async Task ExecuteSearchAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(400, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        SetPublicState(loading: true);

        try
        {
            var results = await _playfieldService.SearchPublicPlayfieldsAsync(query, ct);

            SearchLoadingIndicator.IsRunning = false;
            SearchLoadingIndicator.IsVisible = false;

            if (results.Count == 0)
            {
                PublicResultsList.IsVisible = false;
                SearchEmptyLabel.IsVisible = true;
            }
            else
            {
                PublicResultsList.ItemsSource = results;
                PublicResultsList.IsVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded — do nothing; newer search will update the UI
        }
        catch
        {
            SearchLoadingIndicator.IsRunning = false;
            SearchLoadingIndicator.IsVisible = false;
            SearchErrorLabel.IsVisible = true;
        }
    }

    private void SetPublicState(bool loading)
    {
        SearchLoadingIndicator.IsRunning = loading;
        SearchLoadingIndicator.IsVisible = loading;
        SearchPromptLabel.IsVisible = false;
        SearchEmptyLabel.IsVisible = false;
        SearchErrorLabel.IsVisible = false;
        PublicResultsList.IsVisible = false;
    }

    private void ShowPublicPrompt()
    {
        SearchLoadingIndicator.IsRunning = false;
        SearchLoadingIndicator.IsVisible = false;
        SearchPromptLabel.IsVisible = true;
        SearchEmptyLabel.IsVisible = false;
        SearchErrorLabel.IsVisible = false;
        PublicResultsList.IsVisible = false;
        PublicResultsList.ItemsSource = null;
    }

    private async void OnPublicPlayfieldTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not Playfield playfield)
            return;
        await Shell.Current.GoToAsync(
            $"{AppShell.PlayfieldDetailsRoute}?id={Uri.EscapeDataString(playfield.Id)}&readonly=true");
    }

    private void CancelSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }
}

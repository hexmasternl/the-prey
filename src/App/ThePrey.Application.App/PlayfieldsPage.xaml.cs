using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public partial class PlayfieldsPage : ContentPage
{
    private readonly IPlayfieldService _playfieldService;
    private readonly PlayfieldCacheService _cache;
    private List<Playfield> _playfields = [];

    public PlayfieldsPage(IPlayfieldService playfieldService, PlayfieldCacheService cache)
    {
        InitializeComponent();
        _playfieldService = playfieldService;
        _cache = cache;

        Title = AppLocalizer.PlayfieldsPageTitle;
        CreateNewButton.Text = AppLocalizer.PlayfieldsCreateNew;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPlayfieldsAsync();
    }

    private async Task LoadPlayfieldsAsync()
    {
        SetState(loading: true);
        try
        {
            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                var fetched = await _playfieldService.GetPlayfieldsAsync();
                _playfields = fetched.ToList();
                await _cache.SaveAsync(_playfields);
            }
            else
            {
                _playfields = (await _cache.LoadAsync()).ToList();
                if (_playfields.Count == 0)
                {
                    ShowEmpty(AppLocalizer.PlayfieldsOfflineEmpty);
                    return;
                }
            }
        }
        catch (UnauthorizedException)
        {
            SetState(loading: false);
            await Shell.Current.GoToAsync(AppShell.LoginRoute);
            return;
        }
        catch
        {
            // Server unreachable — fall back to cache
            _playfields = (await _cache.LoadAsync()).ToList();
            if (_playfields.Count == 0)
            {
                ShowEmpty(AppLocalizer.PlayfieldsOfflineEmpty);
                return;
            }
        }

        SetState(loading: false);
        PlayfieldsList.ItemsSource = _playfields;
        PlayfieldsList.IsVisible = true;
    }

    private void SetState(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        EmptyStateLabel.IsVisible = false;
        PlayfieldsList.IsVisible = false;
    }

    private void ShowEmpty(string message)
    {
        LoadingIndicator.IsRunning = false;
        LoadingIndicator.IsVisible = false;
        EmptyStateLabel.Text = message;
        EmptyStateLabel.IsVisible = true;
        PlayfieldsList.IsVisible = false;
    }

    private async void OnCreateNewClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("playfield-details");

    private async void OnPlayfieldTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not Playfield playfield)
            return;
        await Shell.Current.GoToAsync($"playfield-details?id={Uri.EscapeDataString(playfield.Id)}");
    }

    private async void OnDeleteSwipeInvoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not Playfield playfield)
            return;

        var confirmed = await DisplayAlert(
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
            // Rebind to refresh the CollectionView
            PlayfieldsList.ItemsSource = null;
            PlayfieldsList.ItemsSource = _playfields;
        }
        catch (UnauthorizedException)
        {
            await Shell.Current.GoToAsync(AppShell.LoginRoute);
        }
        catch
        {
            await DisplayAlert(AppLocalizer.Error, AppLocalizer.PlayfieldsDeleteError, AppLocalizer.Ok);
        }
    }
}

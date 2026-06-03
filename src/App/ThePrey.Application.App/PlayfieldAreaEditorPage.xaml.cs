using System.Text.Json;
using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public partial class PlayfieldAreaEditorPage : ContentPage
{
    private readonly PlayfieldEditingContext _editingContext;
    private List<PlayfieldCoordinate> _coordinates = [];
    private bool _initSent;

    public PlayfieldAreaEditorPage(PlayfieldEditingContext editingContext)
    {
        InitializeComponent();
        _editingContext = editingContext;

        CancelButton.Text = AppLocalizer.Cancel;
        OkButton.Text = AppLocalizer.Ok;
    }

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _initSent = false;
        _coordinates = [];
        OkButton.IsEnabled = false;
    }

    // ─── HybridWebView initialisation ────────────────────────────────────────

    private void OnRawMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Message)) return;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(e.Message); }
        catch { return; }

        var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;

        switch (type)
        {
            case "ready":
                if (!_initSent)
                {
                    _initSent = true;
                    _ = SendInitAsync();
                }
                break;

            case "update":
                if (doc.RootElement.TryGetProperty("coordinates", out var coordsEl))
                {
                    _coordinates = coordsEl.EnumerateArray()
                        .Select(c => new PlayfieldCoordinate
                        {
                            Latitude = c.GetProperty("lat").GetDouble(),
                            Longitude = c.GetProperty("lon").GetDouble()
                        })
                        .ToList();
                    MainThread.BeginInvokeOnMainThread(() =>
                        OkButton.IsEnabled = _coordinates.Count >= 3);
                }
                break;
        }
    }

    private async Task SendInitAsync()
    {
        var existing = _editingContext.CurrentCoordinates;
        object? center = null;

        if (existing.Count == 0)
        {
            var loc = await GetUserLocationAsync();
            if (loc is not null)
                center = new { lat = loc.Latitude, lon = loc.Longitude };
        }

        var payload = new
        {
            type = "init",
            coordinates = existing.Select(c => new { lat = c.Latitude, lon = c.Longitude }),
            center
        };

        var json = JsonSerializer.Serialize(payload);
        MainThread.BeginInvokeOnMainThread(() => MapWebView.SendRawMessage(json));
    }

    private static async Task<Microsoft.Maui.Devices.Sensors.Location?> GetUserLocationAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted) return null;

            return await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5)));
        }
        catch { return null; }
    }

    // ─── Button handlers ─────────────────────────────────────────────────────

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnOkClicked(object? sender, EventArgs e)
    {
        _editingContext.CurrentCoordinates = [.. _coordinates];
        await Shell.Current.GoToAsync("..");
    }
}

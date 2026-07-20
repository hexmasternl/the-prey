namespace HexMaster.ThePrey.Maui.App.Pages;

/// <summary>
/// Full-screen, non-dismissable consent wall pushed as a modal by <c>LocationConsentGate</c> on iOS
/// when the player has declined the background-location disclosure (a programmatic app-quit, used on
/// Android/Windows/Mac instead, is disallowed by Apple's review guidelines). There is no decline path:
/// the supplied <paramref name="completion"/> resolves — and consent is persisted by the caller — only
/// when the player taps Accept.
/// </summary>
public partial class LocationConsentWallPage : ContentPage
{
    private readonly TaskCompletionSource _completion;

    public LocationConsentWallPage(TaskCompletionSource completion)
    {
        InitializeComponent();
        _completion = completion;
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        _completion.TrySetResult();
        await Navigation.PopModalAsync();
    }

    // Consent is mandatory here — block the Android hardware back button so the wall cannot be
    // dismissed without an Accept tap. (iOS has no hardware back button; harmless there.)
    protected override bool OnBackButtonPressed() => true;
}

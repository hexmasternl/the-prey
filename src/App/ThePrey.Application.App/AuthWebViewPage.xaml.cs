namespace ThePrey.Application.App;

public partial class AuthWebViewPage : ContentPage
{
    private readonly string _endUrl;
    private readonly TaskCompletionSource<string?> _tcs;

    public AuthWebViewPage(string startUrl, string endUrl, TaskCompletionSource<string?> tcs)
    {
        InitializeComponent();
        _endUrl = endUrl;
        _tcs = tcs;
        Title = AppLocalizer.AuthSignInTitle;
        CancelItem.Text = AppLocalizer.Cancel;
        AuthWebView.Source = startUrl;
    }

    // Catches iOS swipe-to-dismiss and any path that closes the page without auth completing.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _tcs.TrySetResult(null);
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith(_endUrl, StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        CompleteAuth(e.Url);
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        // Fallback: some platforms raise Navigated but not Navigating for custom-scheme redirects.
        if (!e.Url.StartsWith(_endUrl, StringComparison.OrdinalIgnoreCase))
            return;

        CompleteAuth(e.Url);
    }

    private void CompleteAuth(string url)
    {
        _tcs.TrySetResult(url);
        MainThread.BeginInvokeOnMainThread(() => _ = Navigation.PopModalAsync(animated: true));
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync(animated: true);
    }
}

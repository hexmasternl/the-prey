using Auth0.OidcClient;

namespace ThePrey.Application.App;

public partial class LandingPage : ContentPage
{
    private CancellationTokenSource? _animationCts;

    public LandingPage()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;

        _ = RunEntranceAsync(token);
        _ = RunSweepAsync(token);
        _ = RunHaloPulseAsync(token);
        _ = RunScanlineAsync(token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = null;

        // Stop any in-flight transforms so cancelled loops unwind cleanly.
        foreach (var view in new VisualElement[] { SweepImage, TitleHalo, ScanLine })
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(view);
    }

    private void ApplyLocalization()
    {
        TitleLabel.Text = AppLocalizer.AppTitle;
        CatchyPhraseLabel.Text = AppLocalizer.CatchyPhrase;
        CreateAccountButton.Text = AppLocalizer.CreateAccountButton;
        LoginButton.Text = AppLocalizer.LoginButton;
    }

    // Staggered fade + rise for the wordmark and actions.
    private async Task RunEntranceAsync(CancellationToken ct)
    {
        VisualElement[] sequence =
        {
            EyebrowLabel, TitleLabel, Divider, CatchyPhraseLabel,
            CreateAccountButton, LoginButton
        };

        foreach (var view in sequence)
        {
            view.Opacity = 0;
            view.TranslationY = 26;
        }

        try
        {
            await Task.Delay(180, ct).ConfigureAwait(true);

            foreach (var view in sequence)
            {
                if (ct.IsCancellationRequested) return;
                _ = view.FadeToAsync(1, 520, Easing.CubicOut);
                await view.TranslateToAsync(0, 0, 520, Easing.CubicOut).ConfigureAwait(true);
                await Task.Delay(90, ct).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Page left the screen mid-animation.
        }
        finally
        {
            // Guarantee the final resting state regardless of how we exit.
            foreach (var view in sequence)
            {
                view.Opacity = 1;
                view.TranslationY = 0;
            }
        }
    }

    // Continuous radar sweep rotating around the backdrop's lock point.
    private async Task RunSweepAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await SweepImage.RotateToAsync(360, 4200, Easing.Linear).ConfigureAwait(true);
                // 360° ≡ 0°, so resetting is visually seamless for the next loop.
                SweepImage.Rotation = 0;
            }
        }
        catch (OperationCanceledException)
        {
            // Page left the screen mid-animation.
        }
    }

    // Slow "breathing" of the green halo behind the wordmark.
    private async Task RunHaloPulseAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.WhenAll(
                    TitleHalo.ScaleToAsync(1.12, 1800, Easing.SinInOut),
                    TitleHalo.FadeToAsync(0.85, 1800, Easing.SinInOut)).ConfigureAwait(true);

                if (ct.IsCancellationRequested) return;

                await Task.WhenAll(
                    TitleHalo.ScaleToAsync(1.0, 1800, Easing.SinInOut),
                    TitleHalo.FadeToAsync(0.5, 1800, Easing.SinInOut)).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Page left the screen mid-animation.
        }
    }

    // Scan bar sweeping top-to-bottom on a loop.
    private async Task RunScanlineAsync(CancellationToken ct)
    {
        try
        {
            // Wait for layout so we know how far to travel.
            while (RootGrid.Height <= 0 && !ct.IsCancellationRequested)
                await Task.Delay(120, ct).ConfigureAwait(true);

            while (!ct.IsCancellationRequested)
            {
                var distance = RootGrid.Height;
                ScanLine.TranslationY = 0;
                await ScanLine.FadeToAsync(0.6, 280, Easing.CubicIn).ConfigureAwait(true);
                await ScanLine.TranslateToAsync(0, distance, 3400, Easing.Linear).ConfigureAwait(true);
                await ScanLine.FadeToAsync(0, 280, Easing.CubicOut).ConfigureAwait(true);
                await Task.Delay(900, ct).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Page left the screen mid-animation.
        }
    }

    private async void OnCreateAccountClicked(object? sender, EventArgs e)
    {
        var auth0Client = GetAuth0Client();
        if (auth0Client is null) return;

        try
        {
            CreateAccountButton.IsEnabled = false;
            LoginButton.IsEnabled = false;

            var result = await auth0Client.LoginAsync(new { screen_hint = "signup", audience = MauiProgram.Auth0Audience });

            if (!result.IsError)
                await HandleSuccessfulLogin(result.AccessToken);
            else
                await DisplayAlertAsync("Error", result.ErrorDescription ?? result.Error, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            CreateAccountButton.IsEnabled = true;
            LoginButton.IsEnabled = true;
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var auth0Client = GetAuth0Client();
        if (auth0Client is null) return;

        try
        {
            CreateAccountButton.IsEnabled = false;
            LoginButton.IsEnabled = false;

            var result = await auth0Client.LoginAsync(new { audience = MauiProgram.Auth0Audience });

            if (!result.IsError)
                await HandleSuccessfulLogin(result.AccessToken);
            else
                await DisplayAlertAsync("Error", result.ErrorDescription ?? result.Error, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            CreateAccountButton.IsEnabled = true;
            LoginButton.IsEnabled = true;
        }
    }

    private static Auth0Client? GetAuth0Client() =>
        IPlatformApplication.Current?.Services.GetService<Auth0Client>();

    private static Task HandleSuccessfulLogin(string? accessToken)
    {
        // TODO: navigate to the main app shell after a successful login
        return Task.CompletedTask;
    }
}

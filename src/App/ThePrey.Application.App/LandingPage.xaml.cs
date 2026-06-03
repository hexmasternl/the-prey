using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public partial class LandingPage : ContentPage
{
    private CancellationTokenSource? _animationCts;

    public LandingPage()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    private static IAuthService? Auth => IPlatformApplication.Current?.Services.GetService<IAuthService>();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;

        _ = RunSweepAsync(token);
        _ = RunHaloPulseAsync(token);
        _ = RunScanlineAsync(token);
        _ = StartAsync(token);
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
        RestoringLabel.Text = AppLocalizer.RestoringSession;
        CreateAccountButton.Text = AppLocalizer.CreateAccountButton;
        LoginButton.Text = AppLocalizer.LoginButton;
    }

    // Brand entrance runs while we attempt to restore a remembered session in the background.
    // If restore succeeds we close this page and return to the main menu; otherwise we reveal
    // the login actions.
    private async Task StartAsync(CancellationToken ct)
    {
        var restoreTask = Auth?.RestoreSessionAsync() ?? Task.FromResult(false);

        await RunFadeRiseAsync(
            new VisualElement[] { EyebrowLabel, TitleLabel, Divider, CatchyPhraseLabel }, ct);

        bool restored;
        try { restored = await restoreTask; }
        catch { restored = false; }

        if (ct.IsCancellationRequested)
            return;

        if (restored)
        {
            await GoToMainAsync();
            return;
        }

        RestoreStack.IsVisible = false;
        LoginActions.IsVisible = true;
        await RunFadeRiseAsync(new VisualElement[] { CreateAccountButton, LoginButton }, ct);
    }

    // Staggered fade + rise for a group of elements.
    private static async Task RunFadeRiseAsync(VisualElement[] views, CancellationToken ct)
    {
        foreach (var view in views)
        {
            view.Opacity = 0;
            view.TranslationY = 26;
        }

        try
        {
            foreach (var view in views)
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
            foreach (var view in views)
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
        => await AttemptLoginAsync(signUp: true);

    private async void OnLoginClicked(object? sender, EventArgs e)
        => await AttemptLoginAsync(signUp: false);

    private async Task AttemptLoginAsync(bool signUp)
    {
        var auth = Auth;
        if (auth is null) return;

        try
        {
            CreateAccountButton.IsEnabled = false;
            LoginButton.IsEnabled = false;

            if (await auth.LoginAsync(signUp))
                await GoToMainAsync();
            else
                await DisplayAlertAsync("Login failed", "Could not sign you in. Please try again.", "OK");
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

    // Closes the login/welcome page and returns to the main menu.
    private static Task GoToMainAsync() => Shell.Current.GoToAsync("..");
}

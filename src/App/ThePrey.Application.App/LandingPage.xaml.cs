using Auth0.OidcClient;

namespace ThePrey.Application.App;

public partial class LandingPage : ContentPage
{
    private CancellationTokenSource? _animationCts;
    private readonly Random _random = new();

    public LandingPage()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _animationCts = new CancellationTokenSource();
        _ = AnimateCrosshairAsync(_animationCts.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = null;
    }

    private void ApplyLocalization()
    {
        CatchyPhraseLabel.Text = AppLocalizer.CatchyPhrase;
        CreateAccountButton.Text = AppLocalizer.CreateAccountButton;
        LoginButton.Text = AppLocalizer.LoginButton;
    }

    private async Task AnimateCrosshairAsync(CancellationToken ct)
    {
        // Allow the layout to measure before reading dimensions
        await Task.Delay(300, ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            var pageWidth = Width;
            var pageHeight = Height;

            if (pageWidth > 0 && pageHeight > 0)
            {
                const double imgSize = 160.0;
                var newX = _random.NextDouble() * Math.Max(0, pageWidth - imgSize);
                var newY = _random.NextDouble() * Math.Max(0, pageHeight - imgSize);

                await CrosshairImage.TranslateToAsync(newX, newY, 2200, Easing.CubicInOut)
                    .ConfigureAwait(false);
            }

            var delayMs = _random.Next(800, 2000);
            try
            {
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
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

            var result = await auth0Client.LoginAsync(new { screen_hint = "signup" });

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

            var result = await auth0Client.LoginAsync();

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

using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the login page. Delegates the interactive Auth0 sign-in to
/// <see cref="IInteractiveLoginService"/> and maps its outcome to page UI: on success it re-enters
/// the startup bootstrap; on cancel it stays on the page; on failure it shows a retryable message.
/// </summary>
public sealed class LoginViewModel : ObservableObject
{
    private readonly IInteractiveLoginService _login;
    private readonly ILogger<LoginViewModel> _logger;

    private bool _isBusy;
    private string? _errorMessage;

    public LoginViewModel(IInteractiveLoginService login, ILogger<LoginViewModel> logger)
    {
        _login = login;
        _logger = logger;
        LoginCommand = new Command(async () => await LoginAsync(), () => !IsBusy);
    }

    public Command LoginCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                LoginCommand.ChangeCanExecute();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var outcome = await _login.LoginAsync();
            switch (outcome)
            {
                case InteractiveLoginOutcome.Success:
                    // Re-enter the startup bootstrap, which will route to the main menu.
                    await Shell.Current.GoToAsync("//welcome");
                    break;

                case InteractiveLoginOutcome.Cancelled:
                    // User dismissed the browser — stay on the login page, no error.
                    break;

                case InteractiveLoginOutcome.NoRefreshToken:
                    ErrorMessage = "Sign-in succeeded but the app was not granted a refresh token. " +
                                   "Enable 'Allow Offline Access' for the API in Auth0, then try again.";
                    break;

                default:
                    ErrorMessage = "Sign-in failed. Please try again.";
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login page failed to complete sign-in.");
            ErrorMessage = "Something went wrong during sign-in. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

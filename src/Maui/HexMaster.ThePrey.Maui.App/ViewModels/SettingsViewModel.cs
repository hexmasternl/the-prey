using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the settings page: loads the current user's display name + language, auto-saves the display
/// name (300 ms debounced) and the language toggle (immediate + live UI switch), and exposes
/// loading/saving/error state. Plain .NET (MAUI/HTTP concerns are behind interfaces) so it is fully
/// unit-testable, including the debounce (via an injected <see cref="TimeProvider"/>).
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    /// <summary>The two supported UI language codes.</summary>
    private const string English = "en";
    private const string Dutch = "nl";

    /// <summary>Debounce window for the display-name auto-save.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly IUserApiClient _userApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILocalizationService _localization;
    private readonly ILanguageStore _languageStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SettingsViewModel> _logger;

    // Suppresses the auto-save triggers while LoadAsync populates values from the server.
    private bool _suppressAutoSave;

    private CancellationTokenSource? _nameSaveCts;
    private CancellationTokenSource? _languageSaveCts;

    private string _displayName = string.Empty;
    private string _selectedLanguage = English;
    private bool _isBusy;
    private bool _hasLoadError;
    private bool _isSaving;
    private bool _isSaved;
    private bool _hasSaveError;
    private bool _displayNameRequired;

    public SettingsViewModel(
        IUserApiClient userApi,
        IAccessTokenProvider accessTokenProvider,
        ILocalizationService localization,
        ILanguageStore languageStore,
        TimeProvider timeProvider,
        ILogger<SettingsViewModel> logger)
    {
        _userApi = userApi;
        _accessTokenProvider = accessTokenProvider;
        _localization = localization;
        _languageStore = languageStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>The editable display name. Editing schedules a debounced save.</summary>
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value) && !_suppressAutoSave)
                ScheduleNameSave();
        }
    }

    /// <summary>The selected language code (<c>en</c>/<c>nl</c>). Switching applies + saves immediately.</summary>
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                OnPropertyChanged(nameof(IsDutch));
                if (!_suppressAutoSave)
                    ApplyLanguageChange(value);
            }
        }
    }

    /// <summary>Two-state convenience for the EN/NL toggle.</summary>
    public bool IsDutch
    {
        get => string.Equals(SelectedLanguage, Dutch, StringComparison.OrdinalIgnoreCase);
        set => SelectedLanguage = value ? Dutch : English;
    }

    /// <summary>True while the initial load is in flight.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>True when the settings could not be loaded (no session / backend error).</summary>
    public bool HasLoadError
    {
        get => _hasLoadError;
        private set => SetProperty(ref _hasLoadError, value);
    }

    /// <summary>True while a save is in flight.</summary>
    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    /// <summary>True after a save succeeds (until the next edit).</summary>
    public bool IsSaved
    {
        get => _isSaved;
        private set => SetProperty(ref _isSaved, value);
    }

    /// <summary>True when the last save failed.</summary>
    public bool HasSaveError
    {
        get => _hasSaveError;
        private set => SetProperty(ref _hasSaveError, value);
    }

    /// <summary>True when the display name is blank (nothing is sent).</summary>
    public bool DisplayNameRequired
    {
        get => _displayNameRequired;
        private set => SetProperty(ref _displayNameRequired, value);
    }

    /// <summary>
    /// Loads the current user's settings on appearing. No token or any non-success outcome shows the
    /// load-error state. On success, populates the fields and aligns the app language to the stored
    /// preference; the auto-save triggers are suppressed while populating so loading does not save.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        HasLoadError = false;
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
            {
                HasLoadError = true;
                return;
            }

            var result = await _userApi.GetCurrentUserAsync(token, ct);
            if (result.Outcome == UserSettingsOutcome.Success && result.Settings is not null)
            {
                PopulateFromServer(result.Settings);
            }
            else
            {
                if (result.Outcome == UserSettingsOutcome.Unauthorized)
                    _accessTokenProvider.Invalidate();
                HasLoadError = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user settings.");
            HasLoadError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateFromServer(UserSettings settings)
    {
        _suppressAutoSave = true;
        try
        {
            DisplayName = settings.DisplayName;
            var language = Normalize(settings.PreferredLanguage);
            SelectedLanguage = language;
            // Align the running app language to the account's stored preference and persist it.
            _localization.SetLanguage(language);
            _languageStore.SetLanguage(language);
        }
        finally
        {
            _suppressAutoSave = false;
        }

        ClearSaveStatus();
        DisplayNameRequired = false;
    }

    private void ScheduleNameSave()
    {
        CancelPendingNameSave();
        var cts = new CancellationTokenSource();
        _nameSaveCts = cts;
        _ = DebouncedNameSaveAsync(cts.Token);
    }

    private async Task DebouncedNameSaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(DebounceDelay, _timeProvider, ct);
        }
        catch (TaskCanceledException)
        {
            return; // Superseded by a newer keystroke.
        }

        var name = DisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            // The backend requires a non-empty display name — send nothing and flag it.
            DisplayNameRequired = true;
            ClearSaveStatus();
            return;
        }

        DisplayNameRequired = false;
        await SaveAsync(name, SelectedLanguage, ct);
    }

    private void ApplyLanguageChange(string code)
    {
        var language = Normalize(code);

        // Apply + persist locally first so the UI switches instantly and a failed save never reverts it.
        _localization.SetLanguage(language);
        _languageStore.SetLanguage(language);

        var name = DisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            // No valid name to persist alongside the language — keep the local switch, flag the name.
            DisplayNameRequired = true;
            return;
        }

        CancelPendingLanguageSave();
        var cts = new CancellationTokenSource();
        _languageSaveCts = cts;
        _ = SaveAsync(name, language, cts.Token);
    }

    private async Task SaveAsync(string name, string language, CancellationToken ct)
    {
        IsSaving = true;
        IsSaved = false;
        HasSaveError = false;

        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
            {
                IsSaving = false;
                HasSaveError = true;
                return;
            }

            var result = await _userApi.UpdateUserAsync(new UserSettings(name, language), token, ct);
            if (ct.IsCancellationRequested)
                return; // Superseded — discard this (possibly stale) response.

            IsSaving = false;
            switch (result.Outcome)
            {
                case SaveSettingsOutcome.Success:
                    IsSaved = true;
                    break;
                case SaveSettingsOutcome.ValidationFailed:
                    DisplayNameRequired = true;
                    HasSaveError = true;
                    break;
                case SaveSettingsOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    HasSaveError = true;
                    break;
                default:
                    HasSaveError = true;
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded — leave state for the newer save to update.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save user settings.");
            IsSaving = false;
            HasSaveError = true;
        }
    }

    private void ClearSaveStatus()
    {
        IsSaving = false;
        IsSaved = false;
        HasSaveError = false;
    }

    private void CancelPendingNameSave()
    {
        _nameSaveCts?.Cancel();
        _nameSaveCts?.Dispose();
        _nameSaveCts = null;
    }

    private void CancelPendingLanguageSave()
    {
        _languageSaveCts?.Cancel();
        _languageSaveCts?.Dispose();
        _languageSaveCts = null;
    }

    private static string Normalize(string? code) =>
        string.Equals(code, Dutch, StringComparison.OrdinalIgnoreCase) ? Dutch : English;
}

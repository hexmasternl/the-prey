namespace HexMaster.ThePrey.Maui.App.Services.Localization;

/// <summary>
/// <see cref="ILanguageStore"/> over MAUI <see cref="Preferences"/>. Touches a MAUI-only API, so it
/// stays behind the interface and is excluded from the plain-.NET test project.
/// </summary>
public sealed class PreferencesLanguageStore : ILanguageStore
{
    private const string LanguageKey = "app_language";

    private readonly IPreferences _preferences;

    public PreferencesLanguageStore(IPreferences preferences) => _preferences = preferences;

    public string? GetLanguage()
    {
        var value = _preferences.Get<string?>(LanguageKey, null);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void SetLanguage(string code) => _preferences.Set(LanguageKey, code);
}

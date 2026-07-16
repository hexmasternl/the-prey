namespace HexMaster.ThePrey.Maui.App.Services.Localization;

/// <summary>Persists the user's chosen UI language locally (thin seam over MAUI Preferences).</summary>
public interface ILanguageStore
{
    /// <summary>The stored language code (<c>en</c>/<c>nl</c>), or <c>null</c> if none is stored.</summary>
    string? GetLanguage();

    /// <summary>Persists the chosen language code.</summary>
    void SetLanguage(string code);
}

using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace HexMaster.ThePrey.Maui.App.Services.Localization;

/// <summary>
/// Default <see cref="ILocalizationService"/> wrapping a <see cref="ResourceManager"/> over the
/// app's string resources. <see cref="SetLanguage"/> swaps the active culture and raises the
/// indexer <see cref="INotifyPropertyChanged"/> so all <c>Translate</c>-bound elements re-render.
/// Plain .NET (no MAUI types) so it is unit-testable with any <see cref="ResourceManager"/>.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    // The conventional indexer property name; raising it refreshes every indexer binding.
    private const string IndexerName = "Item[]";

    private readonly ResourceManager _resourceManager;

    public LocalizationService(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
        CurrentCulture = CultureInfo.InvariantCulture;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo CurrentCulture { get; private set; }

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key))
                return key ?? string.Empty;

            try
            {
                // GetString already walks the fallback chain (specific culture → neutral), so a key
                // missing only in the culture-specific resource returns the neutral/English value.
                return _resourceManager.GetString(key, CurrentCulture) ?? key;
            }
            catch (MissingManifestResourceException)
            {
                // No resources at all (e.g. design-time) — never crash, show the key.
                return key;
            }
        }
    }

    public void SetLanguage(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        var culture = CultureInfo.GetCultureInfo(code);
        if (culture.Equals(CurrentCulture))
            return;

        CurrentCulture = culture;
        // Keep thread cultures aligned for any non-bound formatting.
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
    }
}

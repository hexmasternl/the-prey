using System.ComponentModel;
using System.Globalization;

namespace HexMaster.ThePrey.Maui.App.Services.Localization;

/// <summary>
/// App-wide, runtime-switchable string localization. Raises <see cref="INotifyPropertyChanged"/>
/// for the indexer when the language changes so XAML bindings (via the <c>Translate</c> markup
/// extension) re-render live without recreating pages.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    /// <summary>The localized string for <paramref name="key"/>, falling back to the neutral value, then the key.</summary>
    string this[string key] { get; }

    /// <summary>The currently active culture.</summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>Switches the active language (e.g. <c>en</c>/<c>nl</c>) and refreshes all bound strings.</summary>
    void SetLanguage(string code);
}

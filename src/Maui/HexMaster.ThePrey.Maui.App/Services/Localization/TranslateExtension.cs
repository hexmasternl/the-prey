using System.Globalization;

namespace HexMaster.ThePrey.Maui.App.Services.Localization;

/// <summary>
/// XAML markup extension: <c>Text="{loc:Translate Settings_Title}"</c>. Binds the target property
/// to <see cref="ILocalizationService"/>'s indexer for <see cref="Key"/> so the text re-renders
/// when the language changes at runtime. The service is supplied once at startup via
/// <see cref="Localization"/> (markup extensions are not DI-constructed).
/// </summary>
[ContentProperty(nameof(Key))]
public sealed class TranslateExtension : IMarkupExtension<BindingBase>
{
    /// <summary>The active localization service, set once at app startup.</summary>
    public static ILocalizationService? Localization { get; set; }

    /// <summary>The resource key to translate.</summary>
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var source = Localization;
        if (source is null)
        {
            // Design-time / not yet wired: show the key as a static fallback rather than crashing.
            return new Binding(nameof(Key), source: this, mode: BindingMode.OneTime);
        }

        return new Binding($"[{Key}]", BindingMode.OneWay, source: source);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) =>
        ProvideValue(serviceProvider);
}

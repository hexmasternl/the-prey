namespace HexMaster.ThePrey.Maui.App.Services.Localization;

/// <summary>
/// Default <see cref="ILanguageResolver"/>. Precedence: the persisted preference if present, else the
/// device language mapped to a supported code — <c>nl</c> for Dutch devices, <c>en</c> otherwise (the
/// two supported codes; unknown → English). Plain .NET (device language via an injected accessor) so
/// it is unit-testable.
/// </summary>
public sealed class LanguageResolver : ILanguageResolver
{
    /// <summary>The two supported UI language codes.</summary>
    public static readonly string[] Supported = ["en", "nl"];

    private const string Default = "en";

    private readonly ILanguageStore _store;
    private readonly Func<string> _deviceLanguage;

    /// <param name="deviceLanguage">Returns the device's two-letter ISO language code (e.g. <c>nl</c>).</param>
    public LanguageResolver(ILanguageStore store, Func<string> deviceLanguage)
    {
        _store = store;
        _deviceLanguage = deviceLanguage;
    }

    public string Resolve()
    {
        var stored = _store.GetLanguage();
        if (stored is not null && Array.Exists(Supported, s => string.Equals(s, stored, StringComparison.OrdinalIgnoreCase)))
            return stored.ToLowerInvariant();

        var device = _deviceLanguage() ?? Default;
        return string.Equals(device, "nl", StringComparison.OrdinalIgnoreCase) ? "nl" : Default;
    }
}

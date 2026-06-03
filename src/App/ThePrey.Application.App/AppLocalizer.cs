using System.Globalization;
using System.Resources;

[assembly: NeutralResourcesLanguage("en")]

namespace ThePrey.Application.App;

internal static class AppLocalizer
{
    private static readonly ResourceManager ResourceManager = new(
        "ThePrey.Application.App.Resources.Strings.AppResources",
        typeof(AppLocalizer).Assembly);

    public static string AppTitle => Get(nameof(AppTitle));
    public static string CatchyPhrase => Get(nameof(CatchyPhrase));
    public static string LoginButton => Get(nameof(LoginButton));
    public static string CreateAccountButton => Get(nameof(CreateAccountButton));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}

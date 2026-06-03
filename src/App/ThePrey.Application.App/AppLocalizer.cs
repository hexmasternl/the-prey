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
    public static string RestoringSession => Get(nameof(RestoringSession));
    public static string PlayButton => Get(nameof(PlayButton));
    public static string PlayfieldsButton => Get(nameof(PlayfieldsButton));
    public static string FriendsButton => Get(nameof(FriendsButton));
    public static string QuitButton => Get(nameof(QuitButton));
    public static string PlayfieldsPageTitle => Get(nameof(PlayfieldsPageTitle));
    public static string PlayfieldsCreateNew => Get(nameof(PlayfieldsCreateNew));
    public static string PlayfieldsOfflineEmpty => Get(nameof(PlayfieldsOfflineEmpty));
    public static string PlayfieldsDeleteTitle => Get(nameof(PlayfieldsDeleteTitle));
    public static string PlayfieldsDeleteMessage => Get(nameof(PlayfieldsDeleteMessage));
    public static string PlayfieldsDeleteConfirm => Get(nameof(PlayfieldsDeleteConfirm));
    public static string PlayfieldsDeleteError => Get(nameof(PlayfieldsDeleteError));
    public static string Cancel => Get(nameof(Cancel));
    public static string Ok => Get(nameof(Ok));
    public static string Error => Get(nameof(Error));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}

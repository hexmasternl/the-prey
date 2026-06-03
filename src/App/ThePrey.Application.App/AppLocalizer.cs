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
    public static string ViewPlayfieldTitle => Get(nameof(ViewPlayfieldTitle));
    public static string EditPlayfieldTitle => Get(nameof(EditPlayfieldTitle));
    public static string NewPlayfieldTitle => Get(nameof(NewPlayfieldTitle));
    public static string SetAreaButton => Get(nameof(SetAreaButton));
    public static string SaveButton => Get(nameof(SaveButton));
    public static string NameValidationMessage => Get(nameof(NameValidationMessage));
    public static string LocationUnavailableNotice => Get(nameof(LocationUnavailableNotice));
    public static string PlayfieldNotFoundError => Get(nameof(PlayfieldNotFoundError));
    public static string SaveError => Get(nameof(SaveError));
    public static string NameLabel => Get(nameof(NameLabel));
    public static string VisibilityLabel => Get(nameof(VisibilityLabel));
    public static string AreaLabel => Get(nameof(AreaLabel));
    public static string TabPrivate => Get(nameof(TabPrivate));
    public static string TabPublic => Get(nameof(TabPublic));
    public static string SearchPlaceholder => Get(nameof(SearchPlaceholder));
    public static string SearchPrompt => Get(nameof(SearchPrompt));
    public static string SearchEmpty => Get(nameof(SearchEmpty));
    public static string SearchError => Get(nameof(SearchError));
    public static string SavedLocallyPending => Get(nameof(SavedLocallyPending));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}

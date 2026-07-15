namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Shell-backed <see cref="ILobbyNavigator"/>. Navigates to the gameplay route once a game starts. The
/// gameplay page is a placeholder until the separate gameplay change replaces it; the route is registered
/// in <c>AppShell</c>.
/// </summary>
public sealed class ShellLobbyNavigator : ILobbyNavigator
{
    /// <summary>Shell route for the gameplay screen the lobby hands off to on start.</summary>
    public const string GameplayRoute = "gameplay";

    public Task GoToGameplayAsync() => Shell.Current.GoToAsync(GameplayRoute);
}

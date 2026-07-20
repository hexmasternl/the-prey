namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>Default <see cref="IMenuNavigator"/> backed by the app <see cref="Shell"/>.</summary>
public sealed class ShellMenuNavigator : IMenuNavigator
{
    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);

    /// <summary>
    /// Navigates to <paramref name="route"/> replacing the current page rather than stacking on top of it.
    /// The leading <c>..</c> pops the current page before pushing the target, so the page left behind is
    /// gone from the navigation stack (and cannot be reached with the back button). An absolute <c>//</c>
    /// route is not an option here: these are <see cref="Routing.RegisterRoute"/> targets, not
    /// <c>ShellContent</c> — the shell declares only the <c>welcome</c> content.
    /// </summary>
    public Task ReplaceAsync(string route) => Shell.Current.GoToAsync($"../{route}");
}

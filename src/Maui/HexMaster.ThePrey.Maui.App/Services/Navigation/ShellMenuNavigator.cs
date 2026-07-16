namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>Default <see cref="IMenuNavigator"/> backed by the app <see cref="Shell"/>.</summary>
public sealed class ShellMenuNavigator : IMenuNavigator
{
    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
}

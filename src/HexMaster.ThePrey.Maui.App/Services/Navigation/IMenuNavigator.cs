namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Abstracts Shell route navigation so view models stay free of MAUI types and remain unit-testable.
/// </summary>
public interface IMenuNavigator
{
    Task GoToAsync(string route);
}

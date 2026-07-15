namespace HexMaster.ThePrey.Maui.App.Services.Platform;

/// <summary>
/// Hands text to the device's native share sheet. Behind an interface so view models stay free of MAUI
/// platform types and remain unit-testable. Dismissing the sheet is a no-op (never an error).
/// </summary>
public interface IShareService
{
    Task ShareTextAsync(string title, string text);
}

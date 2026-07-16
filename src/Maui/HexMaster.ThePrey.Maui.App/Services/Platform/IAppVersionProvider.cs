namespace HexMaster.ThePrey.Maui.App.Services.Platform;

/// <summary>Supplies the running application's version string (abstracted for testability).</summary>
public interface IAppVersionProvider
{
    string Version { get; }
}

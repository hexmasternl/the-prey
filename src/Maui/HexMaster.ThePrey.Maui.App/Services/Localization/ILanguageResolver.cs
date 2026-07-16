namespace HexMaster.ThePrey.Maui.App.Services.Localization;

/// <summary>Resolves the effective UI language at startup: stored preference, else the device language.</summary>
public interface ILanguageResolver
{
    /// <summary>The effective language code (<c>en</c> or <c>nl</c>).</summary>
    string Resolve();
}

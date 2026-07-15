namespace HexMaster.ThePrey.Maui.App.Services.Platform;

/// <summary>Default <see cref="IAppVersionProvider"/> reading the platform app version.</summary>
public sealed class MauiAppVersionProvider : IAppVersionProvider
{
    public string Version => AppInfo.Current.VersionString;
}

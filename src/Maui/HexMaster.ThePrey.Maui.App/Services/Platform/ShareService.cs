using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace HexMaster.ThePrey.Maui.App.Services.Platform;

/// <summary>
/// <see cref="IShareService"/> over MAUI <see cref="Share"/>. <see cref="Share.RequestAsync(ShareTextRequest)"/>
/// simply returns when the user dismisses the sheet, so no dismissal handling is needed.
/// </summary>
public sealed class ShareService : IShareService
{
    public Task ShareTextAsync(string title, string text) =>
        Share.Default.RequestAsync(new ShareTextRequest(text, title));
}

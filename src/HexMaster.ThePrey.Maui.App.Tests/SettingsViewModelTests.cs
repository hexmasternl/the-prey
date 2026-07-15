using System.Diagnostics;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class SettingsViewModelTests
{
    private readonly Mock<IUserApiClient> _userApi = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<ILanguageStore> _languageStore = new();
    private readonly FakeTimeProvider _time = new();

    private SettingsViewModel CreateSut() => new(
        _userApi.Object,
        _tokens.Object,
        _localization.Object,
        _languageStore.Object,
        _time,
        NullLogger<SettingsViewModel>.Instance);

    private static async Task WaitFor(Func<bool> condition, string because)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < 3000)
            await Task.Delay(10);
        Assert.True(condition(), because);
    }

    // ---- Load (8.4) ----

    [Fact]
    public async Task LoadAsync_ShouldPopulateAndAlignLanguage_OnSuccess()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _userApi.Setup(u => u.GetCurrentUserAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Success(new UserSettings("Ghost", "nl")));

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal("Ghost", sut.DisplayName);
        Assert.Equal("nl", sut.SelectedLanguage);
        Assert.True(sut.IsDutch);
        Assert.False(sut.HasLoadError);
        Assert.False(sut.IsBusy);
        _localization.Verify(l => l.SetLanguage("nl"), Times.AtLeastOnce);
        _languageStore.Verify(s => s.SetLanguage("nl"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task LoadAsync_ShouldNotTriggerSave_WhenPopulatingFromServer()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Success(new UserSettings("Ghost", "nl")));

        var sut = CreateSut();
        await sut.LoadAsync();

        // Let any (erroneously) scheduled debounce fire.
        _time.Advance(SettingsViewModel.DebounceDelay);
        await Task.Delay(50);

        _userApi.Verify(u => u.UpdateUserAsync(It.IsAny<UserSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_ShouldShowLoadError_WhenNoToken()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.True(sut.HasLoadError);
        _userApi.Verify(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("unauthorized")]
    [InlineData("notfound")]
    [InlineData("error")]
    public async Task LoadAsync_ShouldShowLoadError_OnNonSuccess(string kind)
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        var result = kind switch
        {
            "unauthorized" => UserSettingsResult.Unauthorized,
            "notfound" => UserSettingsResult.NotFound,
            _ => UserSettingsResult.Error
        };
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.True(sut.HasLoadError);
    }

    // ---- Debounced name save (8.5) ----

    [Fact]
    public async Task DisplayNameEdit_ShouldSendSingleRequestForFinalValue_AfterDebounce()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        var calls = 0;
        _userApi.Setup(u => u.UpdateUserAsync(It.IsAny<UserSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((UserSettings s, string _, CancellationToken _) =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(SaveSettingsResult.Success(s));
            });

        var sut = CreateSut();
        sut.DisplayName = "G";
        sut.DisplayName = "Gh";
        sut.DisplayName = "Ghost";

        _time.Advance(SettingsViewModel.DebounceDelay);
        await WaitFor(() => calls >= 1, "the debounced save should fire once");
        await Task.Delay(50); // ensure no extra calls slip in

        Assert.Equal(1, calls);
        _userApi.Verify(u => u.UpdateUserAsync(
            It.Is<UserSettings>(s => s.DisplayName == "Ghost"), "token", It.IsAny<CancellationToken>()), Times.Once);
        await WaitFor(() => sut.IsSaved, "success maps to the saved hint");
    }

    [Fact]
    public async Task DisplayNameEdit_ShouldNotSend_WhenBlank_AndSetRequired()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");

        var sut = CreateSut();
        sut.DisplayName = "   "; // whitespace only

        _time.Advance(SettingsViewModel.DebounceDelay);
        await WaitFor(() => sut.DisplayNameRequired, "blank name flags the required hint");

        _userApi.Verify(u => u.UpdateUserAsync(It.IsAny<UserSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisplayNameEdit_ShouldSupersedeInFlightSave_WithNewerValue()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");

        var firstCt = new TaskCompletionSource<CancellationToken>();
        var release = new TaskCompletionSource();
        var callNo = 0;
        _userApi.Setup(u => u.UpdateUserAsync(It.IsAny<UserSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (UserSettings s, string _, CancellationToken ct) =>
            {
                var n = Interlocked.Increment(ref callNo);
                if (n == 1)
                {
                    firstCt.TrySetResult(ct);
                    try { await release.Task.WaitAsync(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
                }
                return SaveSettingsResult.Success(s);
            });

        var sut = CreateSut();
        sut.DisplayName = "First";
        _time.Advance(SettingsViewModel.DebounceDelay);

        var ct1 = await firstCt.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(ct1.IsCancellationRequested);

        // A newer edit must cancel the in-flight save's token.
        sut.DisplayName = "Second";
        Assert.True(ct1.IsCancellationRequested);

        release.TrySetResult();
        _time.Advance(SettingsViewModel.DebounceDelay);
        await WaitFor(() => callNo >= 2, "the newer value should be saved");

        _userApi.Verify(u => u.UpdateUserAsync(
            It.Is<UserSettings>(s => s.DisplayName == "Second"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisplayNameEdit_ShouldFlagError_AndKeepText_OnValidationFailed()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _userApi.Setup(u => u.UpdateUserAsync(It.IsAny<UserSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SaveSettingsResult.ValidationFailed);

        var sut = CreateSut();
        sut.DisplayName = "Ghost";
        _time.Advance(SettingsViewModel.DebounceDelay);

        await WaitFor(() => sut.HasSaveError, "a validation failure surfaces the error hint");
        Assert.Equal("Ghost", sut.DisplayName); // text is not lost
    }

    [Fact]
    public async Task DisplayNameEdit_ShouldInvalidateToken_OnUnauthorized()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _userApi.Setup(u => u.UpdateUserAsync(It.IsAny<UserSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SaveSettingsResult.Unauthorized);

        var sut = CreateSut();
        sut.DisplayName = "Ghost";
        _time.Advance(SettingsViewModel.DebounceDelay);

        await WaitFor(() => sut.HasSaveError, "unauthorized surfaces the error hint");
        _tokens.Verify(t => t.Invalidate(), Times.AtLeastOnce);
    }

    // ---- Language toggle (8.6) ----

    [Fact]
    public async Task LanguageToggle_ShouldSwitchPersistAndSave()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _userApi.Setup(u => u.UpdateUserAsync(It.IsAny<UserSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSettings s, string _, CancellationToken _) => SaveSettingsResult.Success(s));

        var sut = CreateSut();
        // A non-empty name is required for the language save's body. The pending debounce timer never
        // fires because the fake clock is not advanced, so this does not race the language save.
        sut.DisplayName = "Ghost";

        sut.IsDutch = true;

        _localization.Verify(l => l.SetLanguage("nl"), Times.AtLeastOnce);
        _languageStore.Verify(s => s.SetLanguage("nl"), Times.AtLeastOnce);
        await WaitFor(() =>
        {
            try
            {
                _userApi.Verify(u => u.UpdateUserAsync(
                    It.Is<UserSettings>(s => s.PreferredLanguage == "nl"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
                return true;
            }
            catch (MockException)
            {
                return false;
            }
        }, "the language change is saved with the new language");
    }

    [Fact]
    public async Task LanguageToggle_ShouldKeepLocalLanguage_WhenBackendSaveFails()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _userApi.Setup(u => u.UpdateUserAsync(It.IsAny<UserSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SaveSettingsResult.Error);

        var sut = CreateSut();
        sut.DisplayName = "Ghost";

        sut.IsDutch = true;

        await WaitFor(() => sut.HasSaveError, "a failed save surfaces the error hint");
        Assert.True(sut.IsDutch);                 // local language not reverted
        Assert.Equal("nl", sut.SelectedLanguage);
        _localization.Verify(l => l.SetLanguage("nl"), Times.AtLeastOnce);
    }
}

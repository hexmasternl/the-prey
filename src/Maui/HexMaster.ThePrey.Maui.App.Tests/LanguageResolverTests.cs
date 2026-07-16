using HexMaster.ThePrey.Maui.App.Services.Localization;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class LanguageResolverTests
{
    private static LanguageResolver CreateSut(string? storedLanguage, string deviceLanguage)
    {
        var store = new Mock<ILanguageStore>();
        store.Setup(s => s.GetLanguage()).Returns(storedLanguage);
        return new LanguageResolver(store.Object, () => deviceLanguage);
    }

    [Fact]
    public void Resolve_ShouldReturnStoredPreference_WhenPresent()
    {
        // Stored "nl" wins even though the device is English.
        var sut = CreateSut(storedLanguage: "nl", deviceLanguage: "en");

        Assert.Equal("nl", sut.Resolve());
    }

    [Fact]
    public void Resolve_ShouldReturnStoredEnglish_EvenWhenDeviceIsDutch()
    {
        var sut = CreateSut(storedLanguage: "en", deviceLanguage: "nl");

        Assert.Equal("en", sut.Resolve());
    }

    [Fact]
    public void Resolve_ShouldReturnDutch_WhenNoPreferenceAndDutchDevice()
    {
        var sut = CreateSut(storedLanguage: null, deviceLanguage: "nl");

        Assert.Equal("nl", sut.Resolve());
    }

    [Fact]
    public void Resolve_ShouldReturnEnglish_WhenNoPreferenceAndNonDutchDevice()
    {
        var sut = CreateSut(storedLanguage: null, deviceLanguage: "fr");

        Assert.Equal("en", sut.Resolve());
    }

    [Fact]
    public void Resolve_ShouldIgnoreUnsupportedStoredValue_AndFallBackToDevice()
    {
        var sut = CreateSut(storedLanguage: "de", deviceLanguage: "nl");

        Assert.Equal("nl", sut.Resolve());
    }
}

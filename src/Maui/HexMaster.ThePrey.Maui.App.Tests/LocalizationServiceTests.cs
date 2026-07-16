using System.ComponentModel;
using System.Resources;
using HexMaster.ThePrey.Maui.App.Services.Localization;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class LocalizationServiceTests
{
    // A ResourceManager over the test-only TestStrings.resx / TestStrings.nl.resx compiled into this
    // assembly (neutral: Greeting=Hello, NeutralOnly=OnlyEnglish; nl: Greeting=Hallo).
    private static LocalizationService CreateSut() =>
        new(new ResourceManager("HexMaster.ThePrey.Maui.App.Tests.TestStrings", typeof(LocalizationServiceTests).Assembly));

    [Fact]
    public void Indexer_ShouldReturnNeutralValue_ByDefault()
    {
        var sut = CreateSut();

        Assert.Equal("Hello", sut["Greeting"]);
    }

    [Fact]
    public void SetLanguage_ShouldChangeResolvedString()
    {
        var sut = CreateSut();

        sut.SetLanguage("nl");

        Assert.Equal("Hallo", sut["Greeting"]);
    }

    [Fact]
    public void SetLanguage_ShouldRaisePropertyChanged()
    {
        var sut = CreateSut();
        var raised = false;
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, _) => raised = true;

        sut.SetLanguage("nl");

        Assert.True(raised);
    }

    [Fact]
    public void Indexer_ShouldFallBackToNeutral_WhenKeyMissingInCulture()
    {
        var sut = CreateSut();
        sut.SetLanguage("nl");

        // NeutralOnly exists only in the neutral resource — it must fall back, not crash.
        Assert.Equal("OnlyEnglish", sut["NeutralOnly"]);
    }

    [Fact]
    public void Indexer_ShouldReturnKey_WhenKeyIsCompletelyMissing()
    {
        var sut = CreateSut();

        Assert.Equal("DoesNotExist", sut["DoesNotExist"]);
    }
}

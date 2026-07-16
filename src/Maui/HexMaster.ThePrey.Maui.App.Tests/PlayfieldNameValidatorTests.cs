using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class PlayfieldNameValidatorTests
{
    [Theory]
    [InlineData("NL, Amsterdam, City park")]
    [InlineData("FRA, Paris, The Mall")]
    [InlineData("US, New York, Central Park")] // two-letter code
    [InlineData("NL,Amsterdam,City park")]     // no spaces after commas
    public void IsPublishable_ShouldBeTrue_ForValidCountryCityNamePattern(string name)
    {
        Assert.True(PlayfieldNameValidator.IsPublishable(name));
    }

    [Theory]
    [InlineData("Amsterdam park")]             // fewer than three parts
    [InlineData("Nl, Amsterdam, Park")]        // country code not uppercase
    [InlineData("USAA, X, Y")]                 // country code too long (4 letters)
    [InlineData("N, Amsterdam, Park")]         // country code too short (1 letter)
    [InlineData("N1, Amsterdam, Park")]        // country code contains a digit
    [InlineData("NL, Amsterdam")]              // only two parts
    [InlineData("NL, , Park")]                 // blank middle part
    [InlineData("NL, Amsterdam, ")]            // blank last part
    [InlineData("NL, Amsterdam, Park, Extra")] // four parts
    [InlineData("")]                           // empty
    [InlineData("   ")]                         // whitespace only
    public void IsPublishable_ShouldBeFalse_ForInvalidNames(string name)
    {
        Assert.False(PlayfieldNameValidator.IsPublishable(name));
    }

    [Fact]
    public void IsPublishable_ShouldBeFalse_ForNull()
    {
        Assert.False(PlayfieldNameValidator.IsPublishable(null));
    }
}

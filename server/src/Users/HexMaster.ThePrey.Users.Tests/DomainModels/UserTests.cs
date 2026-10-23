using HexMaster.ThePrey.Users.DomainModels;

namespace HexMaster.ThePrey.Users.Tests.DomainModels;

public sealed class UserTests
{
    [Fact]
    public void Create_ShouldSetDisplayNameToFirstName_WhenFirstNameIsProvided()
    {
        var user = User.Create("auth0|123", "Alice", "Smith", "alice@example.com", true, "en");

        Assert.Equal("Alice", user.DisplayName);
    }

    [Fact]
    public void Create_ShouldSetDisplayNameToEmail_WhenFirstNameIsNull()
    {
        var user = User.Create("auth0|123", null, "Smith", "alice@example.com", true, "en");

        Assert.Equal("alice@example.com", user.DisplayName);
    }

    [Fact]
    public void Create_ShouldAssignNewGuid()
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void Create_ShouldDefaultCallsignToDisplayName()
    {
        var user = User.Create("auth0|123", "Alice", "Smith", "alice@example.com", true, "en");

        Assert.Equal("Alice", user.Callsign);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldDefaultPreferredLanguageToEnglish_WhenNotProvided(string? preferredLanguage)
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, preferredLanguage);

        Assert.Equal(User.DefaultLanguage, user.PreferredLanguage);
    }

    [Fact]
    public void Update_ShouldChangeDisplayNameAndPreferredLanguage()
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        user.Update(null, null, "The Phantom", "nl");

        Assert.Equal("The Phantom", user.DisplayName);
        Assert.Equal("nl", user.PreferredLanguage);
    }

    [Fact]
    public void UpdateSettings_ShouldChangeCallsignAndPreferredLanguage()
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        user.UpdateSettings("Night-Hawk_7 &$#@", "nl");

        Assert.Equal("Night-Hawk_7 &$#@", user.Callsign);
        Assert.Equal("nl", user.PreferredLanguage);
    }

    [Fact]
    public void UpdateSettings_ShouldNotChangeDisplayName()
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        user.UpdateSettings("Reaper", "en");

        Assert.Equal("Alice", user.DisplayName);
    }

    [Theory]
    [InlineData("ab")]                                  // too short
    [InlineData("a234567890123456789012345678901")]    // 31 characters — too long
    [InlineData("Sniper!")]                             // disallowed character
    [InlineData("Renard%")]                             // disallowed character
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateSettings_ShouldThrow_WhenCallsignIsInvalid(string callsign)
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        Assert.Throws<ArgumentException>(() => user.UpdateSettings(callsign, "en"));
        Assert.Equal("Alice", user.Callsign);
    }

    [Theory]
    [InlineData("abc")]                                 // exactly 3 characters
    [InlineData("a23456789012345678901234567890")]      // exactly 30 characters
    [InlineData("REA  PER")]
    [InlineData("a-b_c")]
    public void UpdateSettings_ShouldAccept_WhenCallsignIsValid(string callsign)
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        user.UpdateSettings(callsign, "en");

        Assert.Equal(callsign, user.Callsign);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("EN")]
    [InlineData("english")]
    [InlineData("")]
    public void UpdateSettings_ShouldThrow_WhenPreferredLanguageIsNotSupported(string preferredLanguage)
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        Assert.Throws<ArgumentException>(() => user.UpdateSettings("Reaper", preferredLanguage));
        Assert.Equal("en", user.PreferredLanguage);
    }

    [Fact]
    public void SyncFromAuth_ShouldUpdateDerivedDisplayName_WhenUserHasNotCustomisedIt()
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        user.SyncFromAuth("Bob", null, "bob@example.com", true);

        Assert.Equal("Bob", user.DisplayName);
    }

    [Fact]
    public void SyncFromAuth_ShouldPreserveCustomDisplayName_WhenUserHasCustomisedIt()
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");
        user.Update("Alice", null, "The Hawk", "en");

        user.SyncFromAuth("AliceNew", null, "alicenew@example.com", true);

        Assert.Equal("The Hawk", user.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrow_WhenSubjectIdIsEmpty(string subjectId)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create(subjectId, "Alice", null, "alice@example.com", true, "en"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrow_WhenEmailIsEmpty(string email)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create("auth0|123", "Alice", null, email, true, "en"));
    }
}

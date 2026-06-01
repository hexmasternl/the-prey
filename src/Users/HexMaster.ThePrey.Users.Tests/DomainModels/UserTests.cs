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
    public void Update_ShouldChangeDisplayNameAndLanguage()
    {
        var user = User.Create("auth0|123", "Alice", null, "alice@example.com", true, "en");

        user.Update(null, null, "The Phantom", "nl");

        Assert.Equal("The Phantom", user.DisplayName);
        Assert.Equal("nl", user.Language);
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

using System.Text.RegularExpressions;

namespace HexMaster.ThePrey.Users.DomainModels;

public sealed partial class User
{
    /// <summary>The language used when none is provided.</summary>
    public const string DefaultLanguage = "en";

    /// <summary>The languages the app can be presented in.</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = ["en", "nl"];

    public const int CallsignMinLength = 3;
    public const int CallsignMaxLength = 30;

    /// <summary>Alphanumerics, spaces, dashes, underscores, and &amp;$#@; between 3 and 30 characters.</summary>
    [GeneratedRegex(@"^[a-zA-Z0-9 \-_&$#@]{3,30}$")]
    private static partial Regex CallsignPattern();

    public Guid Id { get; private set; }
    public string SubjectId { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The player's in-game name. Defaults to <see cref="DisplayName"/>.</summary>
    public string Callsign { get; private set; } = string.Empty;

    public string EmailAddress { get; private set; } = string.Empty;
    public bool IsEmailVerified { get; private set; }

    /// <summary>The language the app is presented in. Defaults to English.</summary>
    public string PreferredLanguage { get; private set; } = DefaultLanguage;

    private User() { }

    public static User Rehydrate(
        Guid id,
        string subjectId,
        string? firstName,
        string? lastName,
        string displayName,
        string callsign,
        string emailAddress,
        bool isEmailVerified,
        string preferredLanguage)
    {
        return new User
        {
            Id = id,
            SubjectId = subjectId,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = displayName,
            Callsign = callsign,
            EmailAddress = emailAddress,
            IsEmailVerified = isEmailVerified,
            PreferredLanguage = preferredLanguage
        };
    }

    public static User Create(
        string subjectId,
        string? firstName,
        string? lastName,
        string emailAddress,
        bool isEmailVerified,
        string? preferredLanguage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        var displayName = firstName ?? emailAddress;

        return new User
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = displayName,
            Callsign = displayName,
            EmailAddress = emailAddress,
            IsEmailVerified = isEmailVerified,
            PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? DefaultLanguage : preferredLanguage
        };
    }

    public void SyncFromAuth(string? firstName, string? lastName, string emailAddress, bool isEmailVerified)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        var oldDerived = FirstName ?? EmailAddress;

        FirstName = firstName;
        LastName = lastName;
        EmailAddress = emailAddress;
        IsEmailVerified = isEmailVerified;

        // Re-apply default DisplayName rule only when the current DisplayName matches the old derived value.
        // If the user has customised it, we leave it untouched.
        if (DisplayName == oldDerived)
            DisplayName = firstName ?? emailAddress;
    }

    public void Update(string? firstName, string? lastName, string displayName, string preferredLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);

        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        PreferredLanguage = preferredLanguage;
    }

    /// <summary>Updates the player's game settings: the in-game callsign and the app language.</summary>
    public void UpdateSettings(string callsign, string preferredLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callsign);

        if (!CallsignPattern().IsMatch(callsign))
            throw new ArgumentException(
                $"A callsign must be {CallsignMinLength}-{CallsignMaxLength} characters of letters, digits, spaces, dashes, underscores, or &$#@.",
                nameof(callsign));

        if (!SupportedLanguages.Contains(preferredLanguage))
            throw new ArgumentException(
                $"The preferred language must be one of: {string.Join(", ", SupportedLanguages)}.",
                nameof(preferredLanguage));

        Callsign = callsign;
        PreferredLanguage = preferredLanguage;
    }
}

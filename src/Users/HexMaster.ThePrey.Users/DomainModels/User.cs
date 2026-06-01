namespace HexMaster.ThePrey.Users.DomainModels;

public sealed class User
{
    public Guid Id { get; private set; }
    public string SubjectId { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string EmailAddress { get; private set; } = string.Empty;
    public bool IsEmailVerified { get; private set; }
    public string Language { get; private set; } = string.Empty;

    private User() { }

    public static User Create(
        string subjectId,
        string? firstName,
        string? lastName,
        string emailAddress,
        bool isEmailVerified,
        string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        return new User
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = firstName ?? emailAddress,
            EmailAddress = emailAddress,
            IsEmailVerified = isEmailVerified,
            Language = language
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

    public void Update(string? firstName, string? lastName, string displayName, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        Language = language;
    }
}

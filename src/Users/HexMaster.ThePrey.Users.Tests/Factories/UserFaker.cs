using Bogus;
using HexMaster.ThePrey.Users.DomainModels;

namespace HexMaster.ThePrey.Users.Tests.Factories;

internal static class UserFaker
{
    private static readonly Faker _faker = new();

    internal static User CreateValid(
        string? subjectId = null,
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        bool isEmailVerified = true,
        string? language = null)
    {
        return User.Create(
            subjectId ?? $"auth0|{_faker.Random.AlphaNumeric(24)}",
            firstName ?? _faker.Name.FirstName(),
            lastName ?? _faker.Name.LastName(),
            email ?? _faker.Internet.Email(),
            isEmailVerified,
            language ?? _faker.Locale);
    }
}

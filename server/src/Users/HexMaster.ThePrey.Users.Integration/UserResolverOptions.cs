namespace HexMaster.ThePrey.Users.Integration;

public record UserResolverOptions
{
    public string StateStoreName { get; init; } = "statestore";
    public int CacheTtlSeconds { get; init; } = 300;
    public string UsersAppId { get; init; } = "hexmaster-theprey-users-api";
}

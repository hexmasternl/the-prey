using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.Users.Integration;

public static class UserResolverRegistration
{
    public static IServiceCollection AddUserResolver(this IServiceCollection services)
    {
        services.AddOptions<UserResolverOptions>().BindConfiguration("UserResolver");
        services.AddSingleton<IUserResolver, UserResolver>();
        return services;
    }
}

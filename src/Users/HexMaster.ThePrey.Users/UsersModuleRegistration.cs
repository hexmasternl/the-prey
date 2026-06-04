using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.Features.CreateUser;
using HexMaster.ThePrey.Users.Features.GetUser;
using HexMaster.ThePrey.Users.Features.UpdateUser;
using HexMaster.ThePrey.Users.Features.UpdateUserSettings;
using HexMaster.ThePrey.Users.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.Users;

public static class UsersModuleRegistration
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateUserCommand, CreateUserResult>, CreateUserCommandHandler>();
        services.AddScoped<IQueryHandler<GetUserQuery, UserDto?>, GetUserQueryHandler>();
        services.AddScoped<ICommandHandler<UpdateUserCommand, UserDto>, UpdateUserCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateUserSettingsCommand, UserDto>, UpdateUserSettingsCommandHandler>();

        services.AddSingleton<IUserMetrics, UserMetrics>();

        return services;
    }
}

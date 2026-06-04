using HexMaster.ThePrey.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HexMaster.ThePrey.Users.Data.AzureTableStorage;

public static class UsersTableStorageRegistration
{
    /// <summary>The Aspire connection name; must match the Table Storage resource modelled in the AppHost.</summary>
    public const string ConnectionName = "users-tables";

    public static IHostApplicationBuilder AddUsersTableStorage(this IHostApplicationBuilder builder)
    {
        builder.AddAzureTableServiceClient(ConnectionName);
        builder.Services.AddScoped<IUserRepository, AzureTableStorageUserRepository>();
        return builder;
    }
}

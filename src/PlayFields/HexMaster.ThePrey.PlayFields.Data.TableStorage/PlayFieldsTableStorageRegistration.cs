using HexMaster.ThePrey.PlayFields;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HexMaster.ThePrey.PlayFields.Data.TableStorage;

public static class PlayFieldsTableStorageRegistration
{
    /// <summary>The Aspire connection name; must match the Table Storage resource modelled in the AppHost.</summary>
    public const string ConnectionName = "playfields-tables";

    public static IHostApplicationBuilder AddPlayFieldsTableStorage(this IHostApplicationBuilder builder)
    {
        builder.AddAzureTableServiceClient(ConnectionName);
        builder.Services.AddScoped<IPlayFieldRepository, TableStoragePlayFieldRepository>();
        return builder;
    }
}

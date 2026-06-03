using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.PlayFields.Features.CreatePlayField;
using HexMaster.ThePrey.PlayFields.Features.GetPlayField;
using HexMaster.ThePrey.PlayFields.Features.ListPlayFields;
using HexMaster.ThePrey.PlayFields.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.PlayFields;

public static class PlayFieldsModuleRegistration
{
    public static IServiceCollection AddPlayFieldsModule(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreatePlayFieldCommand, CreatePlayFieldResult>, CreatePlayFieldCommandHandler>();
        services.AddScoped<IQueryHandler<GetPlayFieldQuery, PlayFieldDto?>, GetPlayFieldQueryHandler>();
        services.AddScoped<IQueryHandler<ListPlayFieldsQuery, IReadOnlyList<PlayFieldSummaryDto>>, ListPlayFieldsQueryHandler>();

        services.AddSingleton<IPlayFieldMetrics, PlayFieldMetrics>();

        return services;
    }
}

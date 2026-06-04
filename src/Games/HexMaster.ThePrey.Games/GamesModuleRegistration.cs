using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Features.CreateGame;
using HexMaster.ThePrey.Games.Features.GetGame;
using HexMaster.ThePrey.Games.Features.GetGameState;
using HexMaster.ThePrey.Games.Features.JoinGame;
using HexMaster.ThePrey.Games.Features.ListGames;
using HexMaster.ThePrey.Games.Features.RecordPlayerLocation;
using HexMaster.ThePrey.Games.Features.SetHunter;
using HexMaster.ThePrey.Games.Features.StartGame;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.Games;

public static class GamesModuleRegistration
{
    public static IServiceCollection AddGamesModule(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateGameCommand, CreateGameResult>, CreateGameCommandHandler>();
        services.AddScoped<ICommandHandler<JoinGameCommand, JoinGameResult?>, JoinGameCommandHandler>();
        services.AddScoped<ICommandHandler<StartGameCommand, StartGameResult?>, StartGameCommandHandler>();
        services.AddScoped<ICommandHandler<SetHunterCommand, SetHunterResult?>, SetHunterCommandHandler>();
        services.AddScoped<ICommandHandler<RecordPlayerLocationCommand, RecordPlayerLocationResult?>, RecordPlayerLocationCommandHandler>();
        services.AddScoped<IQueryHandler<GetGameQuery, GameDto?>, GetGameQueryHandler>();
        services.AddScoped<IQueryHandler<GetGameStateQuery, GameStateDto?>, GetGameStateQueryHandler>();
        services.AddScoped<IQueryHandler<ListGamesQuery, IReadOnlyList<GameSummaryDto>>, ListGamesQueryHandler>();

        services.AddSingleton<IGameMetrics, GameMetrics>();

        return services;
    }
}

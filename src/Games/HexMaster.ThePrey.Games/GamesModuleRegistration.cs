using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.BackgroundServices;
using HexMaster.ThePrey.Games.Features.CreateGame;
using HexMaster.ThePrey.Games.Features.GetActiveGame;
using HexMaster.ThePrey.Games.Features.GetGame;
using HexMaster.ThePrey.Games.Features.GetGameState;
using HexMaster.ThePrey.Games.Features.GetGameStatus;
using HexMaster.ThePrey.Games.Features.JoinGame;
using HexMaster.ThePrey.Games.Features.ListGames;
using HexMaster.ThePrey.Games.Features.RecordPlayerLocation;
using HexMaster.ThePrey.Games.Features.RemoveLobbyPlayer;
using HexMaster.ThePrey.Games.Features.SetHunter;
using HexMaster.ThePrey.Games.Features.SetReady;
using HexMaster.ThePrey.Games.Features.StartGame;
using HexMaster.ThePrey.Games.Features.UpdateGameSettings;
using HexMaster.ThePrey.Games.Features.UpdateLocationBroadcast;
using HexMaster.ThePrey.Games.Notifications;
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
        services.AddScoped<ICommandHandler<RemoveLobbyPlayerCommand, RemoveLobbyPlayerResult?>, RemoveLobbyPlayerCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateGameSettingsCommand, UpdateGameSettingsResult?>, UpdateGameSettingsCommandHandler>();
        services.AddScoped<ICommandHandler<SetReadyCommand, SetReadyResult?>, SetReadyCommandHandler>();
        services.AddScoped<IQueryHandler<GetGameQuery, GameDto?>, GetGameQueryHandler>();
        services.AddScoped<IQueryHandler<GetGameStateQuery, GameStateDto?>, GetGameStateQueryHandler>();
        services.AddScoped<IQueryHandler<ListGamesQuery, IReadOnlyList<GameSummaryDto>>, ListGamesQueryHandler>();
        services.AddScoped<IQueryHandler<GetActiveGameQuery, GameStatusDto?>, GetActiveGameQueryHandler>();
        services.AddScoped<IQueryHandler<GetGameStatusQuery, GameStatusDto?>, GetGameStatusQueryHandler>();

        services.AddScoped<ICommandHandler<UpdateLocationBroadcastCommand, UpdateLocationBroadcastResult>, UpdateLocationBroadcastCommandHandler>();

        services.AddSingleton<IGameMetrics, GameMetrics>();
        services.AddSingleton<ILobbyEventBus, InProcessLobbyEventBus>();
        services.AddSingleton<IGameEventBus, InProcessGameEventBus>();
        services.AddSingleton<IGameEngineEventBus, InProcessGameEngineEventBus>();

        services.AddHostedService<GameCleanupService>();

        return services;
    }
}

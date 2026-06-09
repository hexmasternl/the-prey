using HexMaster.ThePrey.GameEngine;
using HexMaster.ThePrey.GameEngine.Infrastructure;
using HexMaster.ThePrey.GameEngine.Observability;
using HexMaster.ThePrey.Games.Data.Postgres;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureQueueServiceClient("game-engine-queue");
builder.AddNpgsqlDbContext<GamesDbContext>("games");
builder.Services.AddDbContextFactory<GamesDbContext>();

builder.Services.AddTransient<EngineRequestSigningHandler>();
builder.Services.AddHttpClient("GamesApi", client =>
{
    var url = builder.Configuration["GamesApi:Url"] ?? "http://hexmaster-theprey-games-api";
    client.BaseAddress = new Uri(url);
})
.AddHttpMessageHandler<EngineRequestSigningHandler>();

builder.Services.AddSingleton<IGameEngineMetrics, GameEngineMetrics>();
builder.Services.AddSingleton<GameLocationChecker>();
builder.Services.AddHostedService<GameEngineWorker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(GameEngineActivitySource.SourceName)
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddMeter(GameEngineMetrics.MeterName));

var host = builder.Build();
await host.RunAsync();

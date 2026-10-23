using HexMaster.ThePrey.Games;
using HexMaster.ThePrey.Games.Api.Endpoints;
using HexMaster.ThePrey.Games.Api.Integration;
using HexMaster.ThePrey.Games.Data.Postgres;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Users.Integration;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using ThePrey.Aspire.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddDefaultAuthentication();
builder.AddDefaultCors();

// Web PubSub client bound to the "games" hub — used to mint short-lived, group-scoped client access
// URLs so a game's members can open a Web PubSub WebSocket and join that game's group.
builder.AddAzureWebPubSubServiceClient(
    AspireConstants.Resources.WebPubSub,
    settings => settings.HubName = AspireConstants.Resources.WebPubSubHub);

builder.Services.AddGamesModule();
builder.AddGamesPostgres();
builder.Services.AddUserResolver();
builder.Services.AddScoped<IPlayfieldInfoProvider, PlayfieldInfoProvider>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(GameObservabilityConstants.ActivitySourceName))
    .WithMetrics(metrics => metrics
        .AddMeter(GameObservabilityConstants.MeterName));

var app = builder.Build();

app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GamesDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAppConfigurationRefresh();
app.UseDefaultCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGameEndpoints();
app.MapInternalGameEndpoints();
app.MapGameExportEndpoints();

app.Run();

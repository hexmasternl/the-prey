using HexMaster.ThePrey.Games;
using HexMaster.ThePrey.Games.Api.Endpoints;
using HexMaster.ThePrey.Games.Data.Postgres;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddDefaultAuthentication();

builder.Services.AddGamesModule();
builder.AddGamesPostgres();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(GameObservabilityConstants.ActivitySourceName))
    .WithMetrics(metrics => metrics
        .AddMeter(GameObservabilityConstants.MeterName));

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GamesDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGameEndpoints();

app.Run();

using HexMaster.ThePrey.Users;
using HexMaster.ThePrey.Users.Api.Endpoints;
using HexMaster.ThePrey.Users.Data.InMemory;
using HexMaster.ThePrey.Users.Observability;
using Scalar.AspNetCore;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddDefaultAuthentication();

builder.Services.AddUsersModule();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(UserObservabilityConstants.ActivitySourceName))
    .WithMetrics(metrics => metrics
        .AddMeter(UserObservabilityConstants.MeterName));

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapUserEndpoints();

app.Run();

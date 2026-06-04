using HexMaster.ThePrey.Users;
using HexMaster.ThePrey.Users.Api.Endpoints;
using HexMaster.ThePrey.Users.Data.AzureTableStorage;
using HexMaster.ThePrey.Users.Observability;
using Scalar.AspNetCore;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddDefaultAuthentication();
builder.AddDefaultCors();

builder.Services.AddUsersModule();
builder.AddUsersTableStorage();
builder.Services.AddDaprClient();

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
app.UseDefaultCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapUserEndpoints();
app.MapInternalUserEndpoints();

app.Run();

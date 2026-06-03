using HexMaster.ThePrey.PlayFields;
using HexMaster.ThePrey.PlayFields.Api.Endpoints;
using HexMaster.ThePrey.PlayFields.Data.TableStorage;
using HexMaster.ThePrey.PlayFields.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth0:Domain"];
        options.Audience = builder.Configuration["Auth0:Audience"];
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

builder.Services.AddPlayFieldsModule();
builder.AddPlayFieldsTableStorage();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(PlayFieldObservabilityConstants.ActivitySourceName))
    .WithMetrics(metrics => metrics
        .AddMeter(PlayFieldObservabilityConstants.MeterName));

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

app.MapPlayFieldEndpoints();

app.Run();

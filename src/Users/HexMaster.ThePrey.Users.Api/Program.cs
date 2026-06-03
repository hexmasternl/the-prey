using HexMaster.ThePrey.Users;
using HexMaster.ThePrey.Users.Api.Endpoints;
using HexMaster.ThePrey.Users.Data.InMemory;
using HexMaster.ThePrey.Users.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;
using OpenTelemetry.Trace;

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

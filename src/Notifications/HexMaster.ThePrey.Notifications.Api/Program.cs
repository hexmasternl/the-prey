using HexMaster.ThePrey.Notifications;
using HexMaster.ThePrey.Notifications.Api.Endpoints;
using HexMaster.ThePrey.Notifications.Api.Integration;
using HexMaster.ThePrey.Users.Integration;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using ThePrey.Aspire.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddDefaultAuthentication();
builder.AddDefaultCors();

// Web PubSub client bound to the "games" hub — used both to mint client access URLs (negotiate)
// and to push events to per-game groups (subscription endpoints).
builder.AddAzureWebPubSubServiceClient(
    AspireConstants.Resources.WebPubSub,
    settings => settings.HubName = AspireConstants.Resources.WebPubSubHub);

builder.Services.AddNotificationsModule();
builder.Services.AddUserResolver();
builder.Services.AddScoped<IGameMembershipProvider, GameMembershipProvider>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseDefaultCors();

// Dapr delivers events wrapped in CloudEvents; unwrap before model binding.
app.UseCloudEvents();

app.UseAuthentication();
app.UseAuthorization();

app.MapSubscribeHandler();
app.MapNotificationSubscriptionEndpoints();
app.MapNegotiateEndpoints();

app.Run();

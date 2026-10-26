using HexMaster.ThePrey.Users;
using HexMaster.ThePrey.Users.Api.Endpoints;
using HexMaster.ThePrey.Users.Data.AzureTableStorage;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddDefaultAuthentication();
builder.AddDefaultCors();

builder.Services.AddUsersModule();
builder.AddUsersTableStorage();
builder.Services.AddDaprClient();

var app = builder.Build();

app.MapDefaultEndpoints();

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

app.MapUserEndpoints();
app.MapInternalUserEndpoints();

app.Run();

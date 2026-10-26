using HexMaster.ThePrey.PlayFields;
using HexMaster.ThePrey.PlayFields.Api.Endpoints;
using HexMaster.ThePrey.PlayFields.Data.TableStorage;
using HexMaster.ThePrey.Users.Integration;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddDefaultAuthentication();
builder.AddDefaultCors();

builder.Services.AddPlayFieldsModule();
builder.AddPlayFieldsTableStorage();
builder.Services.AddUserResolver();

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

app.MapPlayFieldEndpoints();
app.MapInternalPlayFieldEndpoints();

app.Run();

# New backend module — scaffold

Copied from the Notifications and PlayFields modules as they exist in the repo. Replace `{Module}` (PascalCase, e.g. `Rewards`) and `{module}` (lowercase, e.g. `rewards`). Check the package versions against the current csproj files before pasting — they move.

## Module project — `HexMaster.ThePrey.{Module}/HexMaster.ThePrey.{Module}.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Core\HexMaster.ThePrey.Core\HexMaster.ThePrey.Core.csproj" />
    <ProjectReference Include="..\HexMaster.ThePrey.{Module}.Abstractions\HexMaster.ThePrey.{Module}.Abstractions.csproj" />
    <!-- only if it publishes/consumes integration events -->
    <ProjectReference Include="..\..\Shared\HexMaster.ThePrey.IntegrationEvents\HexMaster.ThePrey.IntegrationEvents.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.10" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.10" />
    <PackageReference Include="OpenTelemetry.Api" Version="1.16.0" />
  </ItemGroup>

</Project>
```

Abstractions csproj: the same `PropertyGroup`, no references.

## Observability — `HexMaster.ThePrey.{Module}/Observability/`

```csharp
// {Module}ActivitySource.cs
using System.Diagnostics;

namespace HexMaster.ThePrey.{Module}.Observability;

internal static class {Module}ActivitySource
{
    internal const string SourceName = "HexMaster.ThePrey.{Module}";

    internal static readonly ActivitySource Source = new(SourceName);
}

/// <summary>Exposes the OpenTelemetry source/meter names for registration in the host project.</summary>
public static class {Module}ObservabilityConstants
{
    public const string ActivitySourceName = {Module}ActivitySource.SourceName;
    public const string MeterName = {Module}Metrics.MeterName;
}
```

```csharp
// I{Module}Metrics.cs + {Module}Metrics.cs
using System.Diagnostics.Metrics;

namespace HexMaster.ThePrey.{Module}.Observability;

public interface I{Module}Metrics
{
    void RecordSomethingCreated();
}

public class {Module}Metrics : I{Module}Metrics
{
    internal const string MeterName = "HexMaster.ThePrey.{Module}";

    private readonly Counter<long> _somethingCreated;

    public {Module}Metrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _somethingCreated = meter.CreateCounter<long>(
            "{module}.something_created",
            unit: "{thing}",
            description: "Total number of things created");
    }

    public virtual void RecordSomethingCreated() => _somethingCreated.Add(1);
}
```

## Registration — `HexMaster.ThePrey.{Module}/{Module}ModuleRegistration.cs`

```csharp
using HexMaster.ThePrey.{Module}.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.{Module};

public static class {Module}ModuleRegistration
{
    public static IServiceCollection Add{Module}Module(this IServiceCollection services)
    {
        // services.AddScoped<ICommandHandler<XCommand, XResult>, XCommandHandler>();
        services.AddSingleton<I{Module}Metrics, {Module}Metrics>();
        // services.AddIntegrationEvents();   // only if it publishes integration events
        return services;
    }
}
```

## Api project — `HexMaster.ThePrey.{Module}.Api/HexMaster.ThePrey.{Module}.Api.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <PropertyGroup>
    <ContainerRepository>theprey/{module}-api</ContainerRepository>
    <ContainerUser>app</ContainerUser>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Dapr.AspNetCore" Version="1.18.4" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.10" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.10" />
    <PackageReference Include="Microsoft.OpenApi" Version="2.10.0" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.16.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.16.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.16.0" />
    <PackageReference Include="Scalar.AspNetCore" Version="2.16.11" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Aspire\ThePrey.Aspire.ServiceDefaults\ThePrey.Aspire.ServiceDefaults.csproj" />
    <ProjectReference Include="..\..\Core\HexMaster.ThePrey.Core\HexMaster.ThePrey.Core.csproj" />
    <ProjectReference Include="..\..\Users\HexMaster.ThePrey.Users.Integration\HexMaster.ThePrey.Users.Integration.csproj" />
    <ProjectReference Include="..\HexMaster.ThePrey.{Module}\HexMaster.ThePrey.{Module}.csproj" />
    <ProjectReference Include="..\HexMaster.ThePrey.{Module}.Abstractions\HexMaster.ThePrey.{Module}.Abstractions.csproj" />
    <!-- <ProjectReference Include="..\HexMaster.ThePrey.{Module}.Data.TableStorage\HexMaster.ThePrey.{Module}.Data.TableStorage.csproj" /> -->
  </ItemGroup>

</Project>
```

Add `Properties/launchSettings.json` like `Notifications.Api/Properties/launchSettings.json` with free ports.

## `Program.cs`

```csharp
using HexMaster.ThePrey.{Module};
using HexMaster.ThePrey.{Module}.Api.Endpoints;
using HexMaster.ThePrey.Users.Integration;
using Scalar.AspNetCore;
using ThePrey.Aspire.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddDefaultAuthentication();
builder.AddDefaultCors();

builder.Services.Add{Module}Module();
// builder.Add{Module}TableStorage();
builder.Services.AddUserResolver();

// No OpenTelemetry here: AddServiceDefaults registers every HexMaster.ThePrey.* source and meter.

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
// app.UseCloudEvents();          // only when subscribing to Dapr pub/sub

app.UseAuthentication();
app.UseAuthorization();

// app.MapSubscribeHandler();     // only when subscribing to Dapr pub/sub
app.Map{Module}Endpoints();

app.Run();
```

## Endpoints — `HexMaster.ThePrey.{Module}.Api/Endpoints/{Module}Endpoints.cs`

```csharp
namespace HexMaster.ThePrey.{Module}.Api.Endpoints;

public static class {Module}Endpoints
{
    public static IEndpointRouteBuilder Map{Module}Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/{module}")
            .WithTags("{Module}")
            .RequireAuthorization();

        // group.MapGet("/", ...).WithName("...").Produces<...>();

        return app;
    }
}
```

## Table Storage adapter (optional) — `HexMaster.ThePrey.{Module}.Data.TableStorage/`

csproj: reference the module project; packages `Aspire.Azure.Data.Tables` (13.4.6) and `Microsoft.Extensions.Hosting.Abstractions` (10.0.10).

```csharp
public static class {Module}TableStorageRegistration
{
    /// <summary>The Aspire connection name; must match the Table Storage resource modelled in the AppHost.</summary>
    public const string ConnectionName = "{module}-tables";

    public static IHostApplicationBuilder Add{Module}TableStorage(this IHostApplicationBuilder builder)
    {
        builder.AddAzureTableServiceClient(ConnectionName);
        builder.Services.AddScoped<I{Entity}Repository, TableStorage{Entity}Repository>();
        return builder;
    }
}
```

## Solution — `server/src/the-prey.slnx`

```xml
  <Folder Name="/{Module}/">
    <Project Path="{Module}/HexMaster.ThePrey.{Module}/HexMaster.ThePrey.{Module}.csproj" />
    <Project Path="{Module}/HexMaster.ThePrey.{Module}.Abstractions/HexMaster.ThePrey.{Module}.Abstractions.csproj" />
    <Project Path="{Module}/HexMaster.ThePrey.{Module}.Api/HexMaster.ThePrey.{Module}.Api.csproj" />
    <Project Path="{Module}/HexMaster.ThePrey.{Module}.Tests/HexMaster.ThePrey.{Module}.Tests.csproj" />
  </Folder>
```

## AppHost — `AspireConstants.cs` + `AppHost.cs`

```csharp
// AspireConstants.Resources
/// <summary>The {Module} API.</summary>
public const string {Module}Api = "hexmaster-theprey-{module}-api";
public const string {Module}Tables = "{module}-tables";   // if it owns tables
```

```csharp
// AppHost.cs
var {module}Tables = storage.AddTables(AspireConstants.Resources.{Module}Tables);

var {module}Api = builder.AddProject<Projects.HexMaster_ThePrey_{Module}_Api>(AspireConstants.Resources.{Module}Api)
    .WithReference({module}Tables)
    .WithDaprSidecar(opts =>
    {
        opts.WithReference(stateStore);   // IUserResolver caches in the Dapr state store
        // opts.WithReference(pubSub);    // publishes or subscribes to integration events
    })
    .WaitFor({module}Tables);

// inside builder.AddYarp(...).WithConfiguration(yarp => { ... })
yarp.AddRoute("/{module}/{**catch-all}", {module}Api);
```

## Bicep — `server/infrastructure/{module}/main.bicep`

Start from `server/infrastructure/notifications/main.bicep` verbatim, then change:

```bicep
var rgName = 'rg-theprey-{module}-${environmentName}'
var {module}Image = '${registryServer}/theprey/{module}-api:${imageTag}'

module {module}Api '../modules/container-app.bicep' = {
  name: '{module}Api'
  scope: rg
  params: {
    name: 'theprey-{module}-api-${environmentName}'
    location: location
    containerAppsEnvironmentId: acaEnv.id
    registryServer: registryServer
    acrPullIdentityId: acrPullIdentity.id
    image: {module}Image
    landingZone: landingZone
    enableDapr: true
    daprAppId: 'hexmaster-theprey-{module}-api'
    // additionalEnvVars: [ { name: 'ConnectionStrings__{module}-tables', value: tablesEndpoint } ]
  }
}

module serviceAccess '../modules/service-access.bicep' = {
  name: '{module}ServiceAccess'
  scope: resourceGroup(landingZone.resourceGroup)
  params: {
    principalId: {module}Api.outputs.principalId
    appConfigName: landingZone.appConfig
    keyVaultName: landingZone.keyVault
    webPubSubName: landingZone.webPubSub
  }
}

output {module}ApiFqdn string = {module}Api.outputs.fqdn
```

Tables: copy the `storageAccountName`/`playfieldsTablesEndpoint` vars and the `storage-tables.bicep` module call from `server/infrastructure/playfields/main.bicep`. `prod.bicepparam`: copy `server/infrastructure/notifications/prod.bicepparam` unchanged.

Gateway rule — `server/infrastructure/landing-zone/modules/http-route-config.bicep`:

```bicep
var {module}App = 'theprey-{module}-api-${environmentName}'

      {
        description: '{Module} API'
        routes: [
          {
            match: {
              prefix: '/{module}'
            }
          }
        ]
        targets: [
          {
            containerApp: {module}App
          }
        ]
      }
```

The landing zone must be redeployed after the service exists, because the route targets the container app by name.

## Workflow — `.github/workflows/{module}.yml`

Copy `.github/workflows/notifications.yml` and replace every `Notifications`/`notifications`:

- `name: {Module}`, `concurrency.group: {module}-${{ github.ref }}`
- path filters: `server/src/{Module}/**`, `server/infrastructure/{module}/**` (keep the shared ones)
- test: `dotnet test server/src/{Module}/HexMaster.ThePrey.{Module}.Tests/HexMaster.ThePrey.{Module}.Tests.csproj --configuration Release`
- publish: `dotnet publish server/src/{Module}/HexMaster.ThePrey.{Module}.Api/HexMaster.ThePrey.{Module}.Api.csproj ... /t:PublishContainer`
- deploy: `--name theprey-{module}-prod`, `--template-file server/infrastructure/{module}/main.bicep`, `--parameters server/infrastructure/{module}/prod.bicepparam`
- keep the `version` job (`uses: ./.github/workflows/version.yml`) and the `release` job (`uses: ./.github/actions/create-release`, `permissions: contents: write`, `if: github.event_name == 'push'`) exactly as they are.

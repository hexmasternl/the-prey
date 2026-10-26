---
name: the-prey-azure-infra
description: The Prey's Azure and Bicep conventions — the shared landing zone, per-module service deployments (Container Apps, Postgres, Table Storage), managed identities and RBAC, App Configuration (incl. runtime refresh and feature flags), Key Vault, Web PubSub, Service Bus + Dapr components, Redis state store, the managed gateway, the Static Web App, and the GitHub Actions workflows that deploy them with OIDC. Use whenever you add, change or review any .bicep / .bicepparam file, a resource, role assignment, App Configuration key, Dapr component, container app setting or environment variable, or a deploy workflow (landing-zone.yml, games.yml, playfields.yml, users.yml, notifications.yml, deploy-website.yml), and whenever a deployed service fails at startup with 403s or missing config.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Azure Infrastructure (Bicep)

All Azure infra is Bicep, deployed at **subscription scope** (`targetScope = 'subscription'`), one resource group per deployable unit, from GitHub Actions on pushes to `main`. There is one environment: `prod`, region `westeurope` (Postgres in `northeurope`, West Europe is not allowed for it).

## Where things live

| Path | What |
|---|---|
| `server/infrastructure/landing-zone/main.bicep` | Shared platform, RG `rg-theprey-landing-prod`. No `.bicepparam`; deployed with defaults. |
| `server/infrastructure/landing-zone/modules/` | `log-analytics`, `app-insights`, `container-apps-environment`, `http-route-config` (managed gateway), `key-vault`, `app-configuration`, `app-config-values`, `storage-queues`, `acr-pull-identity`, `redis` (+ Dapr `statestore`), `service-bus` (+ Dapr `pubsub`), `web-pubsub` |
| `server/infrastructure/modules/container-app.bicep` | The one container-app module every API uses |
| `server/infrastructure/modules/service-access.bicep` | Data-plane RBAC every API gets (App Config, Key Vault, Web PubSub) |
| `server/infrastructure/modules/storage-tables.bicep` | Storage account + one table + Table Data Contributor for the API |
| `server/infrastructure/{games,playfields,users,notifications}/main.bicep` + `prod.bicepparam` | One API each, RG `rg-theprey-{module}-prod` |
| `server/infrastructure/games/modules/` | `games-data.bicep` (Postgres Flexible Server), `games-app-config.bicep` (`Games:MinimumAppVersion`) |
| `website/infrastructure/main.bicep` + `modules/static-web-app.bicep` | Free Static Web App, RG `rg-theprey-website-prod` |
| `.github/workflows/landing-zone.yml`, `games.yml`, `playfields.yml`, `users.yml`, `notifications.yml`, `deploy-website.yml` | Deploys |
| `server/src/Aspire/ThePrey.Aspire.AppHost/AppHost.cs` | Local equivalent of the cloud topology (see `the-prey-local-dev`) |

The full resource inventory, names and App Configuration keys are in [references/resources.md](references/resources.md).

## Hard rules

1. **Reference existing resources with `existing`, never `resourceId()`.** Anything not deployed by the current template is declared and then used through `.id` / `.properties`:

   ```bicep
   resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
     name: landingZone.acaEnvironment
     scope: resourceGroup(landingZone.resourceGroup)
   }
   // acaEnv.id — not resourceId(landingZone.resourceGroup, 'Microsoft.App/managedEnvironments', ...)
   ```

   The only function-built IDs in the repo are built-in **role definitions** (`subscriptionResourceId('Microsoft.Authorization/roleDefinitions', <guid>)` in `service-access.bicep` and `storage-tables.bicep`); keep that pattern for roles only. When a subscription-scope template needs a property of a cross-scope resource and `existing` trips ARM validation, output it from the owning template and pass it in as a (`@secure()` if needed) parameter.
2. **Data-plane access goes to the container app's system-assigned identity.** `container-app.bicep` gives each app `SystemAssigned, UserAssigned`. The user-assigned `theprey-prod-acr-pull-id` is **only** for pulling images (`registries[].identity`). Every role assignment uses `<app>.outputs.principalId` (system-assigned). **Never set `AZURE_CLIENT_ID`** on a container app: `DefaultAzureCredential` would switch to the ACR-pull identity, which has no data-plane roles, and the app dies at startup with a 403 loading App Configuration (Key Vault and Web PubSub fail too).
3. **Web PubSub has no resource-level CORS.** `Microsoft.SignalRService/webPubSub` has no `cors` property (BCP037); that's SignalR. The WebSocket is authorized by the minted, group-scoped token. CORS for the HTTP APIs (incl. the negotiate/token endpoint) is the `Cors:AllowedOrigins` App Configuration key.
4. **No secrets in outputs, params files or App Configuration values.** Secrets are `@secure()` params from GitHub secrets (e.g. `pgAdminPassword`), container-app `secrets` with `secretRef`, Dapr component `secrets`, or Key Vault (referenced from App Configuration). App Configuration holds endpoints and settings only. Existing `#disable-next-line outputs-should-not-contain-secrets` lines are deliberate; don't add new ones without a reason.
5. **Names are contracts.** Container-app names `theprey-{module}-api-prod` are hard-coded in the gateway (`http-route-config.bicep`); Dapr app ids `hexmaster-theprey-{module}-api` must match `AspireConstants.Resources.*`; the Dapr pub/sub component must be named `pubsub` (`AspireConstants.DaprComponents.PubSub`), the state store `statestore`. Rename one side, rename all.
6. **Landing-zone names live in each module's `prod.bicepparam`** (`landingZone` object). A new landing-zone resource a service needs → add it to the `landingZone` param type in that service's `main.bicep` **and** to every `prod.bicepparam` that uses it.

## Service deployment pattern

Every API `main.bicep` does the same four things — copy `users/main.bicep` (Table Storage) or `games/main.bicep` (Postgres) when adding a module (see `the-prey-new-backend-module`):

1. Create `rg-theprey-{module}-${environmentName}`.
2. Declare `acaEnv`, `acrPullIdentity` and `appInsights` (`Microsoft.Insights/components`, name `landingZone.applicationInsights`) as `existing` in the landing-zone RG.
3. Call `../modules/container-app.bicep` with `name: 'theprey-{module}-api-${environmentName}'`, image `${registryServer}/theprey/{module}-api:${imageTag}`, `landingZone`, `applicationInsightsConnectionString: appInsights.properties.ConnectionString`, `enableDapr: true`, `daprAppId: 'hexmaster-theprey-{module}-api'`, plus `additionalEnvVars` / `additionalSecrets` for its data store (`ConnectionStrings__users-tables`, `ConnectionStrings__Games` via `secretRef`, …).
4. Call `../modules/service-access.bicep` scoped to `resourceGroup(landingZone.resourceGroup)` with the app's `principalId`, plus `storage-tables.bicep` if it uses tables (passwordless: the env var is the table **endpoint**, not a connection string).

`container-app.bicep` takes the Application Insights connection string as a required `@secure()` parameter (it does not look the instance up itself) and always injects `APPLICATIONINSIGHTS_CONNECTION_STRING` (secret `appinsights-connection-string`), `OTEL_SERVICE_NAME` (= the container app name, so each API is its own `cloud_RoleName`), `AZURE_APP_CONFIGURATION_ENDPOINT`, `AppConfigurationHostname` and `Cors__AllowedOrigins`; ingress is external on port 8080; 0.25 CPU / 0.5 Gi; HTTP scale rule at 10 concurrent requests. A new public route also needs a rule in `landing-zone/modules/http-route-config.bicep` (path prefix → container app, no rewrite) and the matching YARP route in `AppHost.cs`.

## App Configuration at runtime

- `ServiceDefaults.AddServiceDefaults()` → `AddAzureAppConfiguration()` loads the store **only when `AppConfigurationHostname` is set** (cloud). Locally it is absent and config comes from the AppHost. It selects all keys with the null label, resolves Key Vault references with the same `DefaultAzureCredential`, and loads feature flags.
- **Refresh** (commit 03bba2c): all key-values refresh every **5 minutes** (`ConfigureRefresh(refresh => refresh.RegisterAll()...)`), feature flags every **15 minutes**. It is pull-based: `app.UseAppConfigurationRefresh()` must stay in each `Program.cs` (all four APIs call it before `UseDefaultCors` / `UseAuthentication`). Code that must see changes reads `IConfiguration[...]` per call (like `CheckAppVersionQueryHandler`) or `IOptionsMonitor<T>`; `IOptions<T>` bound at startup never changes.
- Keys are written by Bicep as `Microsoft.AppConfiguration/configurationStores/keyValues` children of an `existing` store: shared ones in `landing-zone/modules/app-config-values.bicep`, module-owned ones in a module file scoped to the landing-zone RG (pattern: `games/modules/games-app-config.bicep`). Feature flags use the name `.appconfig.featureflag~2F<Name>` and content type `application/vnd.microsoft.appconfig.ff+json;charset=utf-8`, default **off**.
- Watch out: Bicep re-writes a key's value on **every** deploy. `games/main.bicep` currently sets `Games:MinimumAppVersion` to its `1.0.14` default each time `games.yml` runs, undoing any manual raise. The OpenSpec change `app-demand-updates` (task 1.3) removes that call so a promote workflow owns the key — until it lands, raise the minimum through the parameter, not only in the portal.

## Messaging, realtime and state

- **Service Bus** (Standard, topics required) + Dapr component `pubsub` of type `pubsub.azure.servicebus.topics` on the ACA environment (`service-bus.bicep`). Locally the same component name is backed by RabbitMQ (`AppHost.cs`, `components/pubsub.yaml`).
- **Web PubSub** (`Free_F1`, one group per game, hub `games`) in the landing zone. Its endpoint is published as `ConnectionStrings:webpubsub`; services use it with their identity (Web PubSub Service Owner via `service-access.bicep`). There is no local emulator. Protocol rules: `the-prey-realtime-communication`.
- **Redis** (Azure Managed Redis `Balanced_B0`) + Dapr component `statestore` (`state.redis`).
- Both Dapr components use connection strings / keys from `listKeys()` as component secrets and `scopes: []` (visible to every Dapr-enabled app). Keep secrets inside the module; never output them.
- The game-engine redesign's cloud side is **in place**: Service Bus + `pubsub`, Web PubSub in the landing zone, the Notifications container app (Dapr) and the `/notifications` gateway route. The per-game Container Apps Job and `gamestart` queue are gone; `storage-queues.bicep` is still deployed with `queueNames: []`.

## Deploy workflows

| Workflow | Trigger paths (push/PR to `main`) | What it deploys |
|---|---|---|
| `landing-zone.yml` | `server/infrastructure/landing-zone/**`, `server/infrastructure/modules/**` | `validate` (bicep build + `az deployment sub what-if`, also on PRs), then `deploy` (`az deployment sub create --name theprey-lz-prod`) |
| `games.yml`, `playfields.yml`, `users.yml`, `notifications.yml` | module source, `Core`, `Shared`, `ServiceDefaults`, its infra folder, `server/infrastructure/modules/**` | `version` → `test` → `publish` (`dotnet publish /t:PublishContainer` to ACR, tag = semVer or `imageTag` input) → `deploy` (bicep build + `az deployment sub create --parameters <module>/prod.bicepparam imageTag=… registryServer=…`) → `release` (`.github/actions/create-release`) |
| `deploy-website.yml` | `website/**` (push only) | Provisions the Static Web App, builds Hugo, deploys with the SWA token fetched by `az staticwebapp secrets list` |

- **Auth is OIDC** (`permissions: id-token: write`, `azure/login@v3` with client/tenant/subscription ids, no client secret). Two identities: `AZURE_PROD_CLIENTID` / `AZURE_PROD_TENANTID` / `AZURE_PROD_SUBSCRIPTION` deploys Bicep; `ACR_CLIENT_ID` / `ACR_TENANT_ID` / `ACR_SUBSCRIPTION_ID` + `ACR_HOSTNAME` push images. `POSTGRES_ADMIN_PASSWORD` feeds `games.yml`. The deploy principal needs role-assignment write rights on the landing-zone RG (because of `service-access.bicep`).
- Pull requests: services only run tests (the Bicep build is in the `deploy` job, which skips PRs), so run the build yourself. Rollback: rerun a service workflow with an older `imageTag`.
- **Deploy order**: landing zone first; services reference its resources as `existing`.
- Versions come only from `version.yml`; releases only from `.github/actions/create-release` (see CLAUDE.md, *Releases & versioning*).

## Environment facts

- Live API base: the gateway `https://gateway.<env-default-domain>` (output `gatewayFqdn`). `api.theprey.nl` is the JWT audience but `enableApiCustomDomain` is `false` and it has no DNS; enable it only after the A and `asuid` TXT records exist (outputs `gatewayInboundIp`, `gatewayCustomDomainVerificationId`).
- Web domain is `theprey.nl` (Static Web App). `theprey.eu` is dead; `theprey.eu.auth0.com` is the live Auth0 tenant — leave it.

## Verify

```powershell
# Lint/compile every template you touched (repeat per file)
az bicep build --file server/infrastructure/landing-zone/main.bicep
az bicep build --file server/infrastructure/games/main.bicep
az bicep build-params --file server/infrastructure/games/prod.bicepparam
az bicep build --file website/infrastructure/main.bicep

# What-if against prod (needs az login on the prod subscription; never 'create' from a laptop)
az deployment sub what-if --location westeurope --name theprey-lz-whatif `
  --template-file server/infrastructure/landing-zone/main.bicep
az deployment sub what-if --location westeurope --name theprey-users-whatif `
  --template-file server/infrastructure/users/main.bicep `
  --parameters server/infrastructure/users/prod.bicepparam `
  --parameters imageTag=<current-tag> registryServer=<acr-host>

# Rules check: no resourceId() substitutes, no AZURE_CLIENT_ID, no CORS on Web PubSub
git grep -n "resourceId(" -- "*.bicep" | grep -v "subscriptionResourceId('Microsoft.Authorization/roleDefinitions'"
git grep -n "AZURE_CLIENT_ID" -- server .github
git grep -n "cors" -- server/infrastructure/landing-zone/modules/web-pubsub.bicep
```

The `git grep` lines should print nothing (the Web PubSub one may only hit the explanatory comment). Read the what-if: deletes or replacements of the Postgres server, storage accounts or App Configuration store are a stop sign.

## Checklist

1. Cross-template references use `existing`; role definitions are the only `subscriptionResourceId()` use.
2. New data-plane access: role assignment on the app's system-assigned `principalId`, scoped to the target resource, `principalType: 'ServicePrincipal'`, name `guid(target.id, principalId, roleId)`. No `AZURE_CLIENT_ID`.
3. Secrets only via `@secure()` params, `secretRef`, Dapr component secrets or Key Vault — never outputs, `.bicepparam` or App Configuration values.
4. New config value: App Configuration key in Bicep (null label), read per call if it must change at runtime; the local value added in `AppHost.cs` / user secrets.
5. Names in step: container app ↔ gateway rule ↔ YARP route; Dapr app id ↔ `AspireConstants`; `pubsub` / `statestore`.
6. `landingZone` param type and every `prod.bicepparam` updated together; workflow `paths` cover new folders.
7. `az bicep build` (and what-if for landing-zone or data changes) run clean.
8. Docs: update `docs/deployment/azure-deployment.md` / `docs/architecture/` if the topology changed (`the-prey-docs-sync`).

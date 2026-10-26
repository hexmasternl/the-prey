# The Prey — Azure resource inventory (prod)

Derived from the Bicep templates. Suffixes (`gp53pncm`) come from `take(uniqueString(subscription().id), 8)` in the landing zone and are copied into each `prod.bicepparam`.

## Resource groups

| RG | Template | Contents |
|---|---|---|
| `rg-theprey-landing-prod` | `server/infrastructure/landing-zone/main.bicep` | Log Analytics, App Insights, ACA environment + gateway, Key Vault, App Configuration, queue storage account (no queues), ACR-pull identity, Redis, Service Bus, Web PubSub, Dapr components |
| `rg-theprey-games-prod` | `server/infrastructure/games/main.bicep` | Games API container app, Postgres Flexible Server (`northeurope`) |
| `rg-theprey-playfields-prod` | `server/infrastructure/playfields/main.bicep` | PlayFields API container app, storage account (`uniqueString(rgName)`) with table `playfields` |
| `rg-theprey-users-prod` | `server/infrastructure/users/main.bicep` | Users API container app, storage account with table `users` |
| `rg-theprey-notifications-prod` | `server/infrastructure/notifications/main.bicep` | Notifications API container app |
| `rg-theprey-website-prod` | `website/infrastructure/main.bicep` | Static Web App `theprey-website-prod` (Free) |

The container registry (`ACR_HOSTNAME`, e.g. `theprey.azurecr.io`) is not managed by this repo's Bicep.

## Landing-zone resources

| Resource | Name | Notes |
|---|---|---|
| Log Analytics | `theprey-prod-law` | PerGB2018, 30 days |
| Application Insights | `theprey-prod-ai` | Workspace-based; connection string injected into every app as a secret |
| Container Apps environment | `theprey-prod-aca-env` | Logs to Log Analytics |
| Managed gateway | `httpRouteConfigs/gateway` (API `2026-01-01`) | Prefix routes `/users`, `/playfields`, `/games`, `/notifications` → `theprey-{module}-api-prod`, no rewrite; optional custom domain `api.theprey.nl` (off) |
| Key Vault | `theprey-lz-kv-<6>` | RBAC mode, soft delete 7 days |
| App Configuration | `theprey-lz-ac-<8>` | Standard, local auth still enabled |
| Storage (queues) | `thepreylz<8>sq` | Legacy, `queueNames: []` |
| User-assigned identity | `theprey-prod-acr-pull-id` | ACR pull only |
| Redis Enterprise | `theprey-prod-redis` | `Balanced_B0`, HA off, Dapr component `statestore` (`state.redis`) |
| Service Bus | `theprey-prod-sb-<8>` | Standard (topics), Dapr component `pubsub` (`pubsub.azure.servicebus.topics`) |
| Web PubSub | `theprey-prod-wps-<8>` | `Free_F1`; no CORS property exists |

## Container apps

| App | Dapr app id | Data | Extra env |
|---|---|---|---|
| `theprey-games-api-prod` | `hexmaster-theprey-games-api` | Postgres DB `games` | `ConnectionStrings__Games` (secretRef `pg-connection-string`) |
| `theprey-playfields-api-prod` | `hexmaster-theprey-playfields-api` | Table `playfields` | `ConnectionStrings__playfields-tables` = table endpoint |
| `theprey-users-api-prod` | `hexmaster-theprey-users-api` | Table `users` | `ConnectionStrings__users-tables` = table endpoint |
| `theprey-notifications-api-prod` | `hexmaster-theprey-notifications-api` | none | — |

All get `APPLICATIONINSIGHTS_CONNECTION_STRING`, `AZURE_APP_CONFIGURATION_ENDPOINT`, `AppConfigurationHostname`, `Cors__AllowedOrigins`. Min replicas 1 (games, notifications set it explicitly), max 2.

## Role assignments (all on the app's system-assigned identity)

| Role | GUID | Scope | Granted by |
|---|---|---|---|
| App Configuration Data Reader | `516239f1-63e1-4d78-a4de-a74fb236a071` | App Configuration store | `modules/service-access.bicep` |
| Key Vault Secrets User | `4633458b-17de-408a-b874-0445c86b69e6` | Key Vault | `modules/service-access.bicep` |
| Web PubSub Service Owner | `12cf5a90-567b-43ae-8102-96cf46c7d9b4` | Web PubSub | `modules/service-access.bicep` |
| Storage Table Data Contributor | `0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3` | Module storage account | `modules/storage-tables.bicep` |

## App Configuration keys (null label)

| Key | Written by | Read by |
|---|---|---|
| `ConnectionStrings:webpubsub` | `landing-zone/modules/app-config-values.bicep` | Games, Notifications (Web PubSub client) |
| `ConnectionStrings:servicebus` | `landing-zone/modules/app-config-values.bicep` | — (Dapr component has its own connection string) |
| `Cors:AllowedOrigins` | `landing-zone/modules/app-config-values.bicep` | `AddDefaultCors` in every API (overrides the env var) |
| `.appconfig.featureflag/EnableDailyGamesExport` | `landing-zone/modules/app-config-values.bicep` | Games `GET /games/export/today` (default off) |
| `Games:MinimumAppVersion` | `games/modules/games-app-config.bicep` (via `games/main.bicep`, default `1.0.14`) | `CheckAppVersionQueryHandler` (`POST /games/version-checker`) |

Refresh: key-values every 5 min, feature flags every 15 min, triggered by requests through `UseAppConfigurationRefresh()`.

## Local ↔ cloud mapping

| Concern | Local (`AppHost.cs`) | Cloud |
|---|---|---|
| Config | AppHost references / user secrets | App Configuration + Key Vault references |
| Tables | Azurite (`storage` → `users-tables`, `playfields-tables`) | Storage accounts, managed identity |
| Postgres | `AddPostgres("postgres").AddDatabase("games")` | Flexible Server, admin connection string as secret |
| Dapr `pubsub` | RabbitMQ (`pubsub.rabbitmq`) | Service Bus topics |
| Dapr `statestore` | Dapr default state store | Redis Enterprise |
| Web PubSub | No emulator: connection string under `webpubsub` in user secrets | Landing-zone Web PubSub, managed identity |
| Gateway | YARP on port 5000 | ACA managed gateway |

## Why

The Prey is a location-based pursuit game where play happens inside a bounded geographic area. Today there is no way for a player to define that area. We need a backend capability that lets a player draw a play area on a map as a closed polygon of GPS points, name it, optionally share it with other players, persist it, and — critically — determine whether a given GPS coordinate falls inside it. This boundary check is the foundation every future gameplay rule (spawning, capture, out-of-bounds detection) will build on.

## What Changes

- Introduce a new **PlayFields** domain module (`HexMaster.ThePrey.PlayFields`) following the modular-monolith / feature-slice conventions, replacing the current weather-forecast placeholder in the existing `HexMaster.ThePrey.PlayFields.Api` project.
- Add a `PlayField` domain model: an owned, named, closed polygon made of an ordered collection of GPS coordinates where the last point connects back to the first.
- Add a `GpsCoordinate` value object (latitude/longitude with validation).
- Implement `PlayField.IsInPlayfield(GpsCoordinate)` — a point-in-polygon test that returns whether a coordinate lies inside the play field.
- Add API endpoints (Minimal APIs + CQRS handlers) to **create a play field**, **retrieve a play field**, and **list play fields visible to the caller** (the caller's own fields plus public fields).
- Allow a play field to be marked **public** so other players can see it; private fields are visible only to their owner.
- Persist play fields in **Azure Table Storage** via the Aspire Azure Storage hosting integration, with a dedicated Data adapter project (`HexMaster.ThePrey.PlayFields.Data.TableStorage`).
- Wire an Azure Storage (Tables) resource into the Aspire AppHost and reference it from the PlayFields API.
- Add observability (ActivitySource + metrics) and a Tests project mirroring the feature slices.

## Capabilities

### New Capabilities
- `playfields`: Creating, persisting, sharing, and retrieving named closed-polygon play areas defined by GPS coordinates, including the geometric `IsInPlayfield` boundary check and Azure Table Storage persistence.

### Modified Capabilities
<!-- None — this is a greenfield capability. -->

## Impact

- **New projects**: `HexMaster.ThePrey.PlayFields`, `HexMaster.ThePrey.PlayFields.Abstractions`, `HexMaster.ThePrey.PlayFields.Data.TableStorage`, `HexMaster.ThePrey.PlayFields.Tests` (under `src/Playfields/`).
- **Modified projects**: `HexMaster.ThePrey.PlayFields.Api` (remove placeholder, wire module + endpoints + Table Storage client); `ThePrey.Aspire.AppHost` (add Azure Storage resource and reference); `ThePrey.Aspire.ServiceDefaults/AspireConstants.cs` (add PlayFields resource/connection names); `src/the-prey.slnx` (add new projects).
- **New dependencies**: `Aspire.Hosting.Azure.Storage` (AppHost), `Aspire.Azure.Data.Tables` (Data adapter / API).
- **APIs**: New `/playfields` endpoint group (create, get-by-id, list).
- **Scope**: Backend only — no frontend changes in this change.

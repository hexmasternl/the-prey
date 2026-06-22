# Low — Hardcoded design-time Postgres credentials

| | |
|---|---|
| **Severity** | Low |
| **Category** | Secret hygiene |
| **Component** | Games.Data.Postgres — design-time factory |
| **Status** | Open |

## Summary

A literal Postgres connection string with `Username=postgres;Password=postgres` is hardcoded in the EF Core design-time DbContext factory. This is used only for `dotnet ef migrations` scaffolding (the runtime connection comes from Aspire/config), and the value is a conventional local default, not a real secret — hence Low.

## Evidence

`src/Games/HexMaster.ThePrey.Games.Data.Postgres/GamesDbContextFactory.cs:15`:

```csharp
optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=games;Username=postgres;Password=postgres");
```

## Impact

No production credential is exposed. The concern is hygiene: a literal credential in source can normalize the pattern and be copied into a context where it does matter.

## Recommendation

Read the design-time connection string from an environment variable (e.g. `EF_DESIGN_CONNECTION` / `ConnectionStrings__games`) with a clearly local fallback, so no literal credential lives in source.

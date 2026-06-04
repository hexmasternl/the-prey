---
name: feedback-aspire-linter-interference
description: A linter repeatedly rewrites AppHost.cs with invalid Aspire API calls; always use Edit to patch a specific line rather than a full Write to prevent losing the fix.
metadata:
  type: feedback
---

The project has a linter (likely an AI-assisted formatter or Roslyn analyzer) that actively rewrites `AppHost.cs` with fabricated APIs such as `builder.addDaprStateStore(...)` and references to non-existent constants like `AspireConstants.Resources.DaprStateStore`. These are not valid Aspire APIs.

**Why:** The linter interferes with every full-file Write to AppHost.cs, reverting to its own invented API surface.

**How to apply:** When editing AppHost.cs, prefer surgical `Edit` calls that patch only the specific lines that changed. If a full Write is necessary, re-read the file immediately afterward and verify it wasn't re-linted. The correct Dapr wiring in Aspire is `builder.AddDapr()` (from `Aspire.Hosting.Dapr`) and `.WithDaprSidecar()` on a project resource — no state-store Aspire resource is needed; components are loaded from the `dapr/components/` YAML folder.

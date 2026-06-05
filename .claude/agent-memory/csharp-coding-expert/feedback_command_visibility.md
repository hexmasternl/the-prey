---
name: command-visibility
description: Commands and results in this codebase are public sealed record, not internal — aligns with ICommandHandler<TCommand, TResult> interface
metadata:
  type: feedback
---

Despite what ADR 0009 says about "internal" commands, all existing feature commands (CreateGameCommand, StartGameCommand, etc.) are `public sealed record` in this codebase. This is necessary because:
1. `ICommandHandler<TCommand, TResult>.Handle` is a `public` method
2. Making `TCommand` or `TResult` internal causes CS0050/CS0051 accessibility inconsistency errors

**Why:** C# requires return types and parameter types of public methods to also be public.

**How to apply:** Always declare commands, queries, and their result types as `public sealed record` in this project.

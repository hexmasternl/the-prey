# Low — No global exception handler / ProblemDetails

| | |
|---|---|
| **Severity** | Low |
| **Category** | Hardening / information disclosure |
| **Component** | Backend (all modules) |
| **Status** | Open |

## Summary

No module registers `AddProblemDetails()` / `UseExceptionHandler`. Several handlers rethrow on unexpected errors with no global handler to sanitize the response, and there is no standardized error contract.

## Evidence

A search finds no `UseExceptionHandler`, `AddProblemDetails`, or `UseDeveloperExceptionPage` in any API. Handlers that rethrow include `GetNotificationsToken` (`GameEndpoints.cs:468`) and the Web PubSub broadcaster (`WebPubSubBroadcaster.cs:64`).

## Impact

- In **Development**, the framework default surfaces the developer exception page (stack traces) — fine locally, risky if a non-prod environment is ever exposed.
- In **Production**, the default returns a bare 500 (no stack trace), so disclosure is limited — but error responses are inconsistent and unstructured across modules, complicating client handling and masking nothing intentionally.

## Recommendation

1. Register `AddProblemDetails()` and `UseExceptionHandler` in `ServiceDefaults` so every module returns sanitized, consistent `application/problem+json` errors in all environments.
2. Ensure no exception detail (messages, stack traces) is returned to clients outside Development.

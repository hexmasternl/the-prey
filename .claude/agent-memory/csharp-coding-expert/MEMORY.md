# Memory Index

- [Aspire linter interference in AppHost.cs](feedback_aspire_linter.md) — linter rewrites AppHost.cs with bogus APIs; use surgical Edit calls and verify afterward
- [Dapr mockability: use CreateInvokeMethodRequest](feedback_dapr_mockability.md) — DaprClient convenience InvokeMethodAsync overloads are non-virtual; mock via abstract HttpRequestMessage overload
- [Cross-service Dapr pattern](feedback_cross_service_dapr_pattern.md) — port interface in domain + Dapr adapter in .Api + internal endpoint in target service; GpsCoordinateDto name clash requires using alias

---
name: dapr-mockability
description: DaprClient convenience methods like InvokeMethodAsync(HttpMethod, appId, method, ct) are not abstract — use CreateInvokeMethodRequest + abstract InvokeMethodAsync(HttpRequestMessage, ct) for Moq compatibility
metadata:
  type: feedback
---

`DaprClient.InvokeMethodAsync<TResponse>(HttpMethod, appId, methodName, ct)` is a non-virtual convenience method and cannot be mocked with Moq.

**Why:** Castle DynamicProxy can only intercept virtual/abstract methods. The convenience overloads delegate to `CreateInvokeMethodRequest` + the abstract `InvokeMethodAsync<TResponse>(HttpRequestMessage, ct)`.

**How to apply:** In `UserResolver` (and any future Dapr service-invocation code), always use:
```csharp
var request = _dapr.CreateInvokeMethodRequest(HttpMethod.Get, appId, path);
var result = await _dapr.InvokeMethodAsync<TResponse>(request, ct);
```
Then mock the abstract `d.InvokeMethodAsync<TResponse>(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())` overload.

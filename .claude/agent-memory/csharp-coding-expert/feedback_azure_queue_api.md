---
name: azure-queue-api
description: AddAzureQueueClient is deprecated; use AddAzureQueueServiceClient for both GameEngine worker and Games API
metadata:
  type: feedback
---

`AddAzureQueueClient(string name)` from `Aspire.Azure.Storage.Queues` is marked obsolete. Use `AddAzureQueueServiceClient(string name)` instead in both `Program.cs` files that need to access the queue.

**Why:** The old API was deprecated in Aspire 13.x. Using it generates an `[Obsolete]` warning at build time.

**How to apply:** Whenever registering an Azure Storage Queue client via Aspire DI in any `Program.cs`, always use `AddAzureQueueServiceClient`.

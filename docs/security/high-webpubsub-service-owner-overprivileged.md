# High — All APIs granted Web PubSub Service Owner

| | |
|---|---|
| **Severity** | High |
| **Category** | Excessive privilege (RBAC) |
| **Component** | Infra — service-access.bicep |
| **Status** | Open |

## Summary

Every API's managed identity (Games, Users, PlayFields, Notifications) is granted the **Web PubSub Service Owner** role on the shared Web PubSub resource. Service Owner is a broad, management-level role — far more than the token-minting/broadcast operations the services actually perform — and it is assigned even to services (Users, PlayFields) that do not use Web PubSub at all.

## Evidence

`infra/modules/service-access.bicep:16, 53-61`:

```bicep
var webPubSubServiceOwnerRoleId = '12cf5a90-567b-43ae-8102-96cf46c7d9b4' // Web PubSub Service Owner
// ...
resource webPubSubOwner ... = {
  properties: { roleDefinitionId: ...webPubSubServiceOwnerRoleId, principalId: <each app identity> }
}
```

This module is applied to all four APIs, so all four receive Service Owner. Only Games (mints client tokens) and Notifications (broadcasts) interact with Web PubSub.

## Impact

Service Owner allows full management of the Web PubSub instance — connections, groups, and permissions across **all** games, not just the holder's own. If any one of these four app identities is compromised, the attacker can manage or disrupt real-time messaging for every game. Granting it to Users/PlayFields needlessly widens the blast radius.

## Recommendation

1. **Stop assigning Web PubSub roles to services that don't use it** — parameterize `service-access.bicep` so only Games and Notifications get a Web PubSub assignment.
2. **Grant the least-privilege role required.** If the operations needed are token minting + group send, evaluate whether a narrower data-plane role suffices; if Service Owner is genuinely required for the SDK calls in use, document that justification explicitly.
3. Review the other shared roles in this module to confirm each service receives only what it needs (the Key Vault Secrets User / App Configuration Data Reader assignments are already correctly scoped read-only).

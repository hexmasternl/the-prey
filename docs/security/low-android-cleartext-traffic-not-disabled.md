# Low — Cleartext HTTP not explicitly disabled; no cert pinning

| | |
|---|---|
| **Severity** | Low |
| **Category** | Transport security |
| **Component** | Mobile client — Android network config |
| **Status** | Open |

## Summary

There is no Android network security configuration and no explicit `usesCleartextTraffic` setting. Because `CapacitorHttp` is enabled, network traffic uses the native OkHttp stack (bypassing WebView mixed-content protections) and relies on OkHttp's default certificate validation with no pinning. A legacy Cordova `<access origin="*" />` also remains.

## Evidence

- No `android:networkSecurityConfig` / `usesCleartextTraffic` in `src/ThePrey/android/app/src/main/AndroidManifest.xml`; no `res/xml/network_security_config.xml`.
- `src/ThePrey/capacitor.config.ts:20-22` — `CapacitorHttp.enabled: true`.
- `src/ThePrey/android/app/src/main/res/xml/config.xml:3` — legacy `<access origin="*" />`.
- `targetSdkVersion = 36` (`variables.gradle`) means the platform default already blocks cleartext, so this is defense-in-depth.

## Impact

Largely mitigated by the modern `targetSdk` default (cleartext blocked) and by all current endpoints being TLS (`https`/`wss`). The residual risk: nothing *guarantees* cleartext is rejected, a future plain-`http://` URL could succeed on the native HTTP path, and there is no pinning to resist a compromised/installed CA.

## Recommendation

1. Add `res/xml/network_security_config.xml` with `cleartextTrafficPermitted="false"` and reference it via `android:networkSecurityConfig`.
2. Consider certificate pinning for the API gateway (`gateway.*.azurecontainerapps.io`) and `*.auth0.com`.
3. Remove the unused Cordova `<access origin="*" />`.

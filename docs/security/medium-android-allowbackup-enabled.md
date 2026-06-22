# Medium — `android:allowBackup="true"` permits extraction of app data

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Data-at-rest / device security |
| **Component** | Mobile client — AndroidManifest |
| **Status** | Open |

## Summary

The Android app allows backup of its data sandbox. On devices permitting `adb backup` (and via cloud auto-backup), the entire app sandbox — including the WebView `localStorage` holding Auth0 tokens, the IndexedDB cache, and Capacitor Preferences — can be copied off the device without root.

## Evidence

`src/ThePrey/android/app/src/main/AndroidManifest.xml:5`:

```xml
android:allowBackup="true"
```

The data exposed includes Auth0 access/refresh tokens in WebView `localStorage` (see [medium-auth-tokens-in-localstorage](./medium-auth-tokens-in-localstorage.md)), the `the-prey-db` IndexedDB (user profile + playfields, via `app-db.service.ts`), and game-tracking state in `@capacitor/preferences`.

## Impact

An attacker with brief physical access (or access to a device backup) can exfiltrate authentication tokens and personal data to another device, then resume the victim's authenticated session.

## Recommendation

1. Set `android:allowBackup="false"` — the safe default for an app whose WebView storage holds auth tokens.
2. Alternatively, provide `android:dataExtractionRules` / `fullBackupContent` XML that explicitly **excludes** WebView data and Preferences from backup.
3. Combine with moving tokens into secure storage ([medium-auth-tokens-in-localstorage](./medium-auth-tokens-in-localstorage.md)) for defense in depth.

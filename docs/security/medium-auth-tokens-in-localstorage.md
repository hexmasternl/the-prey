# Medium — Auth0 access & refresh tokens stored in localStorage

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Token storage / data-at-rest |
| **Component** | Mobile client — Auth0 configuration |
| **Status** | Open |

## Summary

The Auth0 Angular SDK is configured with `cacheLocation: 'localstorage'` and `useRefreshTokens: true`. In a Capacitor WebView, `localStorage` is unencrypted at rest and readable by any JavaScript running in the WebView. Refresh tokens are long-lived, so their theft is more damaging than an access token.

## Evidence

`src/ThePrey/src/main.ts:39-41`:

```typescript
useRefreshTokens: true,
useRefreshTokensFallback: false,
cacheLocation: 'localstorage',
```

## Impact

- WebView `localStorage` persists in the app's data directory and can be extracted on a rooted device or via backup (compounded by [medium-android-allowbackup-enabled](./medium-android-allowbackup-enabled.md)).
- Any XSS in the WebView could read the tokens (no XSS sinks were found today, but this raises the consequence if one is ever introduced).
- A stolen refresh token allows continued token issuance until it is revoked.

## Recommendation

1. Move token storage to **device secure storage**: use `cacheLocation: 'memory'` plus a custom Auth0 `cache` backed by Capacitor secure storage (Android EncryptedSharedPreferences / iOS Keychain).
2. At minimum, set `android:allowBackup="false"` to prevent backup-based extraction (see linked finding).
3. Keep `useRefreshTokensFallback: false` (already set — avoids the weaker iframe fallback).

> This is a well-known Ionic/Auth0 trade-off; the goal is to get tokens out of plain WebView storage on native builds.

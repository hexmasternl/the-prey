# Invite deep-link association files (hosting task)

The invite link `https://theprey.nl/join/{gameId}` is a **verified Android App Link** and an **iOS
Universal Link**. For the OS to open the app (instead of the browser), two static association files must be
published on `theprey.nl`. These are **not code in this repo** — they are a hosting task tracked in the
`maui-game-join-page` change proposal. Until they are published, the links open the browser and the flow
degrades gracefully.

## Android — `https://theprey.nl/.well-known/assetlinks.json`

Backs the `android:autoVerify="true"` intent-filter on `MainActivity` (scheme `https`, host `theprey.nl`,
path prefix `/join`). The app's package name is `nl.hexmaster.theprey.maui.app` (see `<ApplicationId>` in the
csproj). Replace `SHA256_FINGERPRINT_OF_SIGNING_CERT` with the release/upload signing certificate SHA-256
fingerprint (colon-separated uppercase hex).

```json
[
  {
    "relation": ["delegate_permission/common.handle_all_urls"],
    "target": {
      "namespace": "android_app",
      "package_name": "nl.hexmaster.theprey.maui.app",
      "sha256_cert_fingerprints": ["20:BC:45:07:34:5F:AB:20:B5:DB:AF:EE:65:81:D5:D1:BC:0A:CA:DD:6D:E0:76:C3:07:71:3F:B9:A7:BF:40:F0"]
    }
  }
]
```

Serve it at exactly `https://theprey.nl/.well-known/assetlinks.json` with `Content-Type: application/json`
over HTTPS with no redirects.

## iOS — `https://theprey.nl/.well-known/apple-app-site-association`

Backs the `applinks:theprey.nl` Associated Domains entitlement (`Platforms/iOS/Entitlements.plist`) and the
`ContinueUserActivity` wiring in `AppDelegate`. Replace `TEAMID` with the Apple Developer Team ID; the bundle
id is `nl.hexmaster.theprey.maui.app`.

```json
{
  "applinks": {
    "apps": [],
    "details": [
      {
        "appID": "TEAMID.nl.hexmaster.theprey.maui.app",
        "paths": ["/join/*"]
      }
    ]
  }
}
```

Serve it at exactly `https://theprey.nl/.well-known/apple-app-site-association` (no `.json` extension) with
`Content-Type: application/json` over HTTPS with no redirects.

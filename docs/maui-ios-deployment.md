# Deploying the MAUI app to the Apple App Store

This guide takes the **.NET MAUI app** (`src/Maui/HexMaster.ThePrey.Maui.App`, bundle id
`nl.hexmaster.theprey.maui.app`) from nothing to an automated App Store / TestFlight release pipeline.

> **iOS is meaningfully more involved than Android** (see `docs/maui-deployment.md`):
> - Builds **require a macOS runner** (Xcode). GitHub-hosted macOS minutes bill at **10× the Linux rate**,
>   and a MAUI iOS build takes ~10–20 min — budget for it.
> - Signing uses an Apple **distribution certificate + provisioning profile**, not a single keystore.
> - You need an **Apple Developer Program** membership (US$99/year) and, **once**, a Mac (or `fastlane
>   match`) to create/export the signing certificate.

**Prerequisite already handled:** the iOS `Platforms/iOS/AppDelegate.cs` universal-link handler used a
non-existent `NSUserActivity.WebpageUrl`; it's fixed to `WebPageUrl`, so the iOS target now compiles.

---

## Overview of what you'll create

| Thing | Where | Purpose |
|---|---|---|
| App ID (with Associated Domains) | developer.apple.com → Identifiers | Registers the bundle id + universal-link capability |
| Apple Distribution certificate (`.p12`) | Apple + a Mac Keychain, then a secret | Identifies you as the signer |
| App Store provisioning profile (`.mobileprovision`) | Apple, then a secret | Ties the App ID + cert for distribution |
| App Store Connect app record | appstoreconnect.apple.com | The listing + TestFlight |
| App Store Connect API key (`.p8`) | App Store Connect → Integrations | Lets CI upload without a human |
| `apple-app-site-association` | theprey.nl (already added as a template) | Makes `/join/{id}` open the app |
| 7 GitHub secrets | Repo → Settings → Secrets | Cert + profile + identity + API key |
| `maui-ios-release.yml` | This repo | Builds the signed `.ipa` and uploads to TestFlight |

---

# Part 1 — One-time manual setup

## 1.1 Prerequisites

- An **Apple Developer Program** membership: <https://developer.apple.com/programs/> (US$99/year, can take
  a day or two to activate).
- **A Mac, once**, to create the signing certificate's private key and export the `.p12` (Keychain
  Access). If you have no Mac at all, use [`fastlane match`](https://docs.fastlane.tools/actions/match/)
  to generate/store certs — out of scope here, but it replaces steps 1.3–1.4.
- **Admin** access to this GitHub repository.
- Your **Team ID** (developer.apple.com → Membership → Team ID, a 10-char string like `A1B2C3D4E5`).

## 1.2 Register the App ID (with Associated Domains)

1. developer.apple.com → **Certificates, Identifiers & Profiles → Identifiers → +**.
2. **App IDs → App**, Bundle ID **Explicit** = `nl.hexmaster.theprey.maui.app` (must match
   `<ApplicationId>` in the csproj).
3. Under **Capabilities**, tick **Associated Domains** (backs the `applinks:theprey.nl` entitlement in
   `Platforms/iOS/Entitlements.plist`, so `https://theprey.nl/join/{id}` opens the app). **Continue →
   Register**.

## 1.3 Create + export the Apple Distribution certificate (needs a Mac once)

1. On a Mac, open **Keychain Access → Certificate Assistant → Request a Certificate From a Certificate
   Authority**, enter your email, choose **Saved to disk** → produces a `CertificateSigningRequest.certSigningRequest`.
2. developer.apple.com → **Certificates → + → Apple Distribution**, upload the CSR, **download** the
   `.cer`, and **double-click** it to install into the Mac's login keychain.
3. In Keychain Access, find **"Apple Distribution: <Your Org> (TEAMID)"**, expand it to confirm it has a
   **private key**, right-click → **Export** → save as `distribution.p12` with a strong password.

> Keep `distribution.p12` + its password safe (password manager). This plus its private key is your
> signing identity.

## 1.4 Create the App Store provisioning profile

1. developer.apple.com → **Profiles → + → App Store Connect** (Distribution).
2. Select the App ID `nl.hexmaster.theprey.maui.app`, then the **Apple Distribution** certificate from 1.3.
3. Give it a **name** you'll remember, e.g. `The Prey MAUI App Store`, **Generate**, and **download** the
   `.mobileprovision`. Note the exact **profile name** — CI needs it.

## 1.5 Create the app in App Store Connect

1. <https://appstoreconnect.apple.com> → **Apps → +  → New App**.
2. Platform **iOS**, name, primary language, **Bundle ID** = the App ID from 1.2, and an **SKU** (any
   unique string, e.g. `theprey-maui`). **Create**.
3. Fill the required listing: description, keywords, support URL, **screenshots** for the required device
   sizes, **privacy policy URL**, **App Privacy** (data collection — this app uses **location**), age
   rating, category, and pricing. TestFlight uploads work before the listing is complete, but you can't
   *release to the App Store* until it is.

## 1.6 Create an App Store Connect API key (for CI upload)

1. App Store Connect → **Users and Access → Integrations → App Store Connect API → +**.
2. Name it e.g. `ci-uploader`, role **App Manager**, **Generate**.
3. **Download the `.p8` private key — you can only do this once.** Note the **Key ID** and the **Issuer
   ID** (shown on the same page).

## 1.7 Note the signing identity + profile names

- **Signing identity** (from Keychain, exact string): `Apple Distribution: <Your Org> (TEAMID)`.
- **Provisioning profile name**: the name from 1.4 (e.g. `The Prey MAUI App Store`).

## 1.8 Universal Links — publish `apple-app-site-association`

This repo now includes `website/static/.well-known/apple-app-site-association` (a template). Replace
`TEAMID` with your Team ID so the `appID` reads `TEAMID.nl.hexmaster.theprey.maui.app`, keep `paths`
scoped to `/join/*`, and re-deploy the website. It must be served at
`https://theprey.nl/.well-known/apple-app-site-association` — **no `.json` extension**, `Content-Type:
application/json`, over HTTPS with **no redirects** (the Static Web Apps route added for it enforces the
content type). This is the iOS twin of `assetlinks.json`.

## 1.9 Add the GitHub secrets

Base64-encode the two binary files:

- **macOS:** `base64 -i distribution.p12 | pbcopy` and `base64 -i ThePrey.mobileprovision | pbcopy`
- **Linux:** `base64 -w0 distribution.p12` / `base64 -w0 ThePrey.mobileprovision`

Then in GitHub → **Settings → Secrets and variables → Actions**, add:

| Secret name | Value |
|---|---|
| `APPLE_DISTRIBUTION_CERT_P12_BASE64` | base64 of `distribution.p12` |
| `APPLE_DISTRIBUTION_CERT_PASSWORD` | the `.p12` export password |
| `APPLE_PROVISIONING_PROFILE_BASE64` | base64 of the `.mobileprovision` |
| `APPLE_SIGNING_IDENTITY` | `Apple Distribution: <Your Org> (TEAMID)` |
| `APPLE_PROVISIONING_PROFILE_NAME` | e.g. `The Prey MAUI App Store` |
| `APPSTORE_API_KEY_ID` | the Key ID from 1.6 |
| `APPSTORE_API_ISSUER_ID` | the Issuer ID from 1.6 |
| `APPSTORE_API_PRIVATE_KEY` | the **entire** contents of the `.p8` file |

(That's 8 secrets; the table above rounds "7" for the cert/profile/identity/API set.)

---

# Part 2 — The automated release (after Part 1)

`.github/workflows/maui-ios-release.yml` runs on a **macОС** runner: selects Xcode, installs the
`maui-ios` workload, imports the certificate into a temporary keychain, installs the provisioning
profile, builds a **signed `.ipa`**, and uploads it to **TestFlight** via the App Store Connect API.

## 2.1 Versioning

- **`ApplicationVersion`** (the iOS build number `CFBundleVersion`; must increase per upload to a given
  `CFBundleShortVersionString`) ← `github.run_number`.
- **`ApplicationDisplayVersion`** (`CFBundleShortVersionString`, the "1.0" marketing version) ← the
  `displayVersion` input, or parsed from a `maui-ios-vX.Y.Z` tag.

## 2.2 How to run it

- **Manual:** Actions → **MAUI iOS Release → Run workflow** → set `displayVersion`.
- **By tag:** `git tag maui-ios-v1.1.0 && git push origin maui-ios-v1.1.0`.
- **On main:** auto-runs when `src/Maui/**` changes land (path-filtered), uploading a new TestFlight build.

The uploaded build appears in **App Store Connect → TestFlight** after Apple finishes processing (a few
minutes to ~1 hour). Add it to a TestFlight group to install via the TestFlight app; promote to the App
Store from **App Store Connect → Distribution** when ready.

---

# Part 3 — Verify

- **TestFlight:** the build shows up under TestFlight; install it on a device through the TestFlight app.
- **Universal link:** with a TestFlight/App Store build installed and the AASA live, tapping
  `https://theprey.nl/join/{id}` (e.g. from Notes/Messages) opens the app on the Join page. iOS caches the
  AASA at install time — reinstall after fixing the file.

---

# Part 4 — Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `No signing certificate "iOS Distribution" found` / identity not found | The `.p12` didn't import, wrong password, or `APPLE_SIGNING_IDENTITY` doesn't match the cert's exact name. |
| `No profile matching '…' found` | `APPLE_PROVISIONING_PROFILE_NAME` mismatch, or the profile wasn't installed / doesn't match the App ID + cert. |
| `Provisioning profile doesn't include the Associated Domains entitlement` | Enable **Associated Domains** on the App ID (1.2) and regenerate the profile (1.4). |
| Upload: `Authentication failed` | Bad `APPSTORE_API_*` values, or the key lacks the **App Manager** role. |
| Upload: `redundant binary` / build number already used | Bump `ApplicationVersion` (it comes from `run_number` — a fresh run fixes it). |
| macOS build is slow / expensive | Expected (10× minutes). Restrict triggers (path filter already limits to `src/Maui/**`); consider a self-hosted Mac for frequency. |
| Xcode/SDK mismatch (`requires Xcode X`) | Pin a compatible Xcode in the workflow's `setup-xcode` step to match the .NET 10 iOS workload. |
| Universal link opens Safari, not the app | AASA missing/behind a redirect/wrong content type, wrong `TEAMID`, or app not installed from TestFlight/Store (side-loaded debug builds don't get AASA verification). |

---

## Appendix — local build (on a Mac)

```bash
dotnet workload install maui-ios
dotnet publish src/Maui/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj \
  -f net10.0-ios -c Release -p:RuntimeIdentifier=ios-arm64 \
  -p:ArchiveOnBuild=true \
  -p:ApplicationVersion=2 -p:ApplicationDisplayVersion=1.0 \
  -p:CodesignKey="Apple Distribution: <Your Org> (TEAMID)" \
  -p:CodesignProvision="The Prey MAUI App Store"
# → bin/Release/net10.0-ios/ios-arm64/publish/*.ipa
```

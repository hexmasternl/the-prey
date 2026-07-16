# Deploying the MAUI app to Google Play

This guide takes the **.NET MAUI app** (`src/Maui/HexMaster.ThePrey.Maui.App`, package
`nl.hexmaster.theprey.maui.app`) from nothing to an automated Google Play release pipeline.

> This is a **different app** from the older Ionic/Capacitor client (`src/ThePrey`, package
> `nl.hexmaster.theprey`, shipped by `.github/workflows/android-release.yml`). It gets its **own** Play
> Console listing, its **own** upload keystore, and its **own** workflow
> (`.github/workflows/maui-android-release.yml`). The two never collide.

The work splits into a **one-time manual setup** (Part 1) and then a **fully automated** GitHub Actions
release (Part 2). Do Part 1 once; after that every release is a git tag or a button click.

---

## Overview of what you'll create

| Thing | Where | Purpose |
|---|---|---|
| Upload keystore (`.keystore`) | Your machine, then a GitHub secret | Signs the AAB you upload |
| Play Console app | play.google.com/console | The store listing + tracks |
| Play App Signing key | Managed by Google | The key that signs what users install |
| Service account + JSON key | Google Cloud + Play Console | Lets CI publish without a human |
| 5 GitHub secrets | Repo → Settings → Secrets | Keystore + passwords + SA JSON |
| `maui-android-release.yml` | This repo | Builds + uploads the signed AAB |

---

# Part 1 — One-time manual setup

## 1.1 Prerequisites

- A **Google Play Developer account** (one-time US$25 registration, can take a day to verify):
  <https://play.google.com/console/signup>.
- A **Google Cloud** account (same Google identity is fine).
- **Admin** access to this GitHub repository (to add secrets).
- A local machine with the **JDK** installed (for `keytool`). The .NET SDK already ships one; otherwise
  install Temurin 17. Verify: `keytool -help`.

## 1.2 Generate the upload keystore

Google Play uses **Play App Signing**: Google holds the real *app signing key* that signs the APKs users
install; **you** sign each upload with an *upload key*. This keystore is that upload key.

Run (pick your own strong passwords when prompted):

```bash
keytool -genkeypair -v \
  -keystore theprey-maui-upload.keystore \
  -alias theprey-maui \
  -keyalg RSA -keysize 2048 -validity 10000 \
  -dname "CN=HexMaster, O=HexMaster, L=., ST=., C=NL"
```

- It asks for a **keystore password** and (optionally) a **key password**. Record both. Using the same
  value for each is fine and simplest.
- `-alias theprey-maui` is the **key alias** — you'll need it as a secret.
- `-validity 10000` ≈ 27 years. Play requires the key to be valid well past your last planned release.

> ⚠️ **Back this file and its passwords up somewhere safe (a password manager / secure vault).** If you
> lose the *upload* key you can ask Google to reset it; but treat it as critical. Never commit it to git.

Note the keystore's fingerprints (you may need the upload SHA-256 when registering the upload cert, and
you'll compare against the **app signing** SHA-256 later):

```bash
keytool -list -v -keystore theprey-maui-upload.keystore -alias theprey-maui
```

## 1.3 Create the app in Play Console

1. Go to <https://play.google.com/console> → **Create app**.
2. App name **The Prey** (or as you wish), default language, **App** (not game — or game, your call),
   **Free**, accept the declarations → **Create app**.
3. Play assigns your listing; you set the package name at first upload — it must be
   **`nl.hexmaster.theprey.maui.app`** (matches `<ApplicationId>` in the csproj). It cannot be changed
   later, so get it right.
4. Work through **Dashboard → "Set up your app"**. Google will not let you publish until these are done:
   - **App access** (are any screens behind a login? Provide test credentials if so).
   - **Ads** declaration.
   - **Content rating** (fill the IARC questionnaire).
   - **Target audience and content**.
   - **Data safety** (what data the app collects/shares — location, since this app uses GPS).
   - **Government apps / Financial features / Health** as applicable (usually "no").
   - **Privacy policy URL** (host one on theprey.nl).
   - **Store listing**: title, short + full description, app icon (512×512), feature graphic
     (1024×500), and at least 2–8 phone screenshots.

## 1.4 Enable Play App Signing and do the FIRST release manually

The Play **Publishing API cannot create the very first release** for a brand-new app, and Play App
Signing has to be established. So do the first upload by hand, then let CI take over.

1. Build a signed AAB locally (from the repo root):

   ```bash
   dotnet publish src/Maui/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj \
     -f net10.0-android -c Release \
     -p:AndroidPackageFormat=aab \
     -p:ApplicationVersion=1 \
     -p:ApplicationDisplayVersion=1.0 \
     -p:AndroidKeyStore=true \
     -p:AndroidSigningKeyStore=$PWD/theprey-maui-upload.keystore \
     -p:AndroidSigningKeyAlias=theprey-maui \
     -p:AndroidSigningStorePass=YOUR_STORE_PASSWORD \
     -p:AndroidSigningKeyPass=YOUR_KEY_PASSWORD
   ```

   The signed bundle lands at:
   `src/Maui/HexMaster.ThePrey.Maui.App/bin/Release/net10.0-android/publish/nl.hexmaster.theprey.maui.app-Signed.aab`

2. In Play Console → **Testing → Internal testing → Create new release**.
3. When prompted about Play App Signing, **accept** (let Google generate the app signing key — recommended).
4. Upload the `-Signed.aab`, add release notes, **Save → Review → Start rollout to Internal testing**.
5. Create an internal tester list (Play Console → Internal testing → **Testers**) and add your own Google
   account so you can install from the opt-in link.

## 1.5 Grab the app signing SHA-256 (and finish the deep-link setup)

Play Console → **Test and release → App integrity → App signing**. Copy the **SHA-256 certificate
fingerprint** of the **app signing key** (colon-separated uppercase hex).

Put it into `website/static/.well-known/assetlinks.json`, replacing `"<MAUI_APP_SIGNING_SHA256>"` in the
`nl.hexmaster.theprey.maui.app` statement. This is what makes `https://theprey.nl/join/{id}` open the app
(see the deep-link notes in `src/Maui/HexMaster.ThePrey.Maui.App/Platforms/DEEP_LINKS_HOSTING.md`). Re-deploy
the website afterwards.

> Use the **app signing** SHA-256, **not** the upload key's — verified App Links check what users actually
> have installed, which Google re-signs with the app signing key.

## 1.6 Create the service account (so CI can publish)

1. Play Console → **Setup → API access**.
2. If prompted, **link a Google Cloud project** (create one if needed).
3. Click **Create new service account** → this opens Google Cloud Console.
4. In Google Cloud: **IAM & Admin → Service Accounts → Create service account**
   - Name e.g. `play-publisher-maui`. No project roles are required here (permissions are granted in Play,
     not GCP). **Create and continue → Done**.
5. Open the service account → **Keys → Add key → Create new key → JSON → Create**. A `.json` file
   downloads — this is `PLAY_SERVICE_ACCOUNT_JSON`. **Keep it secret.**
6. Make sure the **Google Play Android Developer API** is enabled for the project:
   Google Cloud Console → **APIs & Services → Enable APIs and services → search "Google Play Android
   Developer API" → Enable**.
7. Back in Play Console → **API access** (or **Users and permissions**): find the service account's email
   (`...@....iam.gserviceaccount.com`), **Grant access**, and give it at least:
   - **Releases**: *Release to testing tracks* (and *Release to production* if you want CI to ship to prod),
     *Manage testing tracks*, *View app information and download bulk reports*.
   - Scope it to **this app** (or account-wide) → **Invite user / Apply**.

> Permission propagation can take a few minutes (occasionally longer). If the first CI publish fails with
> *"The caller does not have permission"*, wait and re-run.

## 1.7 Add the GitHub secrets

Base64-encode the keystore so it can live in a secret:

- **macOS/Linux:** `base64 -w0 theprey-maui-upload.keystore > keystore.b64`
  (macOS without `-w0`: `base64 theprey-maui-upload.keystore | tr -d '\n' > keystore.b64`)
- **Windows PowerShell:**
  `[Convert]::ToBase64String([IO.File]::ReadAllBytes("theprey-maui-upload.keystore")) | Set-Content -NoNewline keystore.b64`

Then in GitHub → **Settings → Secrets and variables → Actions → New repository secret**, add:

| Secret name | Value |
|---|---|
| `MAUI_ANDROID_KEYSTORE_BASE64` | Contents of `keystore.b64` |
| `MAUI_ANDROID_KEYSTORE_PASSWORD` | The keystore (store) password |
| `MAUI_ANDROID_KEY_ALIAS` | `theprey-maui` |
| `MAUI_ANDROID_KEY_PASSWORD` | The key password (same as store if you reused it) |
| `PLAY_SERVICE_ACCOUNT_JSON` | The **entire** contents of the service-account `.json` |

> `PLAY_SERVICE_ACCOUNT_JSON` may already exist for the Ionic app. The same service account works for both
> apps **as long as you granted it access to the MAUI app** in step 1.6. If you'd rather keep them fully
> separate, create a second SA and rename the secret in the workflow.

Delete the local `keystore.b64` (it's sensitive) once the secret is saved.

---

# Part 2 — The automated release (after Part 1)

The workflow `.github/workflows/maui-android-release.yml` (below / already in the repo) builds a signed
AAB on an `ubuntu-latest` runner and uploads it to Google Play.

## 2.1 How versioning works

- **`versionCode`** (Android's integer, must strictly increase every upload) ← `github.run_number`, passed
  as `-p:ApplicationVersion=`. It only ever goes up.
  - ⚠️ Your manual first upload used `versionCode 1`. Make sure the workflow's first automated
    `run_number` is **greater than 1** (it will be, once the workflow has run a couple of times) — or bump
    the manual upload's code, or add an offset (see the `VERSION_CODE_OFFSET` note in the workflow).
- **`versionName`** (the human "1.0" string) ← the `displayVersion` input (default `1.0`), passed as
  `-p:ApplicationDisplayVersion=`. Bump this when you cut a real release.

## 2.2 How to run it

- **Manual (recommended for control):** GitHub → **Actions → MAUI Android Release → Run workflow**, choose
  the **track** (`internal` / `alpha` / `beta` / `production`), the **displayVersion**, and the **status**
  (`draft` or `completed`).
- **By tag:** push a tag matching `maui-v*`, e.g.

  ```bash
  git tag maui-v1.1.0
  git push origin maui-v1.1.0
  ```

  This runs the workflow to the **internal** track with `displayVersion = 1.1.0` (parsed from the tag).

> **First automated run:** if Play still considers the app "draft", set **status = `draft`** and promote
> the release in the Console. Once the app has a live release, `completed` rolls out automatically.

## 2.3 Promoting between tracks

Publish to `internal` first, verify on a device, then either re-run the workflow with a higher track or
promote the release in Play Console (**Testing → Internal → Promote release → Closed/Open/Production**).

---

## Part 3 — Verify the deep link end-to-end

After a build is installed from Play (so it carries the **app signing** key) and `assetlinks.json` is live:

```bash
adb shell pm get-app-links nl.hexmaster.theprey.maui.app   # expect: theprey.nl  ->  verified
adb shell am start -a android.intent.action.VIEW -d "https://theprey.nl/join/00000000-0000-0000-0000-000000000000"
```

The second command should open the MAUI app on the Join page (not the browser).

---

## Part 4 — Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `The caller does not have permission` | SA not granted in Play (1.6) or permissions still propagating. Wait and retry. |
| `Package not found: nl.hexmaster.theprey.maui.app` | The app / first release doesn't exist yet. Do the manual first upload (1.4). |
| `Only releases with status draft may be created on draft app` | App has no live release. Run with **status = draft**, then promote in the Console. |
| `APK signature is invalid` / `not signed` | Wrong keystore/alias/passwords in the secrets, or `AndroidKeyStore=true` missing. |
| `Version code N has already been used` | `run_number`-derived `versionCode` collides with a prior upload. Bump the offset or do one throwaway run. |
| App Link shows `legacy_failure` / `none` in `pm get-app-links` | `assetlinks.json` has the wrong SHA-256 (must be the **app signing** key), wrong package, is behind a redirect, or isn't `application/json`. |
| Build can't find the Android SDK/platform | The runner auto-provisions it; if it fails, add a step to `sdkmanager --install "platforms;android-35"` and set `-p:AcceptAndroidSDKLicenses=true`. |

---

## Appendix — Local release build cheatsheet

```bash
dotnet workload restore src/Maui/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj
dotnet publish src/Maui/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj \
  -f net10.0-android -c Release -p:AndroidPackageFormat=aab \
  -p:ApplicationVersion=2 -p:ApplicationDisplayVersion=1.0 \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=$PWD/theprey-maui-upload.keystore \
  -p:AndroidSigningKeyAlias=theprey-maui \
  -p:AndroidSigningStorePass=*** -p:AndroidSigningKeyPass=***
# → bin/Release/net10.0-android/publish/nl.hexmaster.theprey.maui.app-Signed.aab
```

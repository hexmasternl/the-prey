# The Prey — Google Play Store Publishing Guide

## Prerequisites

Before you start, make sure you have these installed:

- **Node.js** (LTS) + **npm**
- **Java JDK 17** (required by Android tooling)
- **Android Studio** (includes Android SDK and build tools)
- **Capacitor CLI**: `npm install -g @capacitor/cli`
- A **Google Play Developer account** ($25 one-time fee at play.google.com/console)

---

## Phase 1 — Add the Android Platform

Do this once per machine (skip if the `android/` folder already exists in `src/ThePrey`).

```powershell
cd src\ThePrey
npm install
npx ionic build --prod
npx cap add android
```

Then open the Android project to let Android Studio sync dependencies:

```powershell
npx cap open android
```

In Android Studio, wait for the Gradle sync to complete. Close Android Studio after sync.

---

## Phase 2 — Verify App Identity

Open `src/ThePrey/capacitor.config.ts` and confirm:

```ts
appId: 'nl.hexmaster.theprey',   // your Play Store package name — never change after publishing
appName: 'ThePrey',
webDir: 'www'
```

> **Important:** Once published to the Play Store, the `appId` can never be changed.

---

## Phase 3 — Generate a Signing Keystore

You need a keystore to sign releases. **Keep this file and its passwords safe forever** — every future update must be signed with the exact same key.

```powershell
keytool -genkey -v -keystore theprey-release.jks `
  -alias theprey `
  -keyalg RSA `
  -keysize 2048 `
  -validity 10000
```

Answer the prompts (name, organization, country). Note the **keystore password** and **key alias password** you choose.

Store the `.jks` file somewhere safe outside the git repository — **never commit it**.

---

## Phase 4 — Configure Gradle for Signing

In `src/ThePrey/android/`, create a file named `key.properties`. Add `key.properties` to `.gitignore` — **never commit this file**.

```properties
storePassword=YOUR_KEYSTORE_PASSWORD
keyPassword=YOUR_KEY_PASSWORD
keyAlias=theprey
storeFile=C:/path/to/theprey-release.jks
```

Open `src/ThePrey/android/app/build.gradle` and add the signing configuration to the `android { }` block:

```groovy
// At the top of the file, before the android block:
def keystorePropertiesFile = rootProject.file("key.properties")
def keystoreProperties = new Properties()
keystoreProperties.load(new FileInputStream(keystorePropertiesFile))

android {
    // ... existing config ...

    signingConfigs {
        release {
            keyAlias keystoreProperties['keyAlias']
            keyPassword keystoreProperties['keyPassword']
            storeFile file(keystoreProperties['storeFile'])
            storePassword keystoreProperties['storePassword']
        }
    }

    buildTypes {
        release {
            signingConfig signingConfigs.release
            minifyEnabled false
            proguardFiles getDefaultProguardFile('proguard-android.txt'), 'proguard-rules.pro'
        }
    }
}
```

---

## Phase 5 — Build a Release Bundle (AAB)

Google Play requires an **Android App Bundle (`.aab`)**, not a plain APK.

```powershell
# From src\ThePrey
npx ionic build --prod
npx cap sync android

# Build the signed release AAB
cd android
.\gradlew bundleRelease
```

The output file will be at:

```
src\ThePrey\android\app\build\outputs\bundle\release\app-release.aab
```

---

## Phase 6 — Create the App on Google Play Console

1. Go to [play.google.com/console](https://play.google.com/console)
2. Click **Create app**
3. Fill in:
   - **App name:** ThePrey
   - **Default language:** your choice
   - **App or game:** Game
   - **Free or paid:** Free
4. Accept the declarations and click **Create app**

---

## Phase 7 — Complete the Store Listing

In the Play Console under **Grow → Store presence → Main store listing**:

| Field | Requirement |
|---|---|
| Short description | Max 80 characters |
| Full description | Max 4000 characters |
| Phone screenshots | At least 2 (1080×1920 or similar) |
| Feature graphic | 1024×500 JPG/PNG |
| App icon | 512×512 PNG, no rounded corners |

Under **App content**, complete all required sections:

- **Privacy policy** — host a policy page and provide its URL (required)
- **Ads** declaration
- **Content rating** questionnaire
- **Target audience** settings
- **Data safety** form — declare location data usage (the app uses GPS)

---

## Phase 8 — Upload Your AAB

1. In Play Console, go to **Release → Production** (or **Internal testing** for your first upload)
2. Click **Create new release**
3. Under **App bundles**, upload `app-release.aab`
4. Fill in **Release notes** (what's new in this version)
5. Click **Save**, then **Review release**

> **Tip:** Use **Internal testing** for your first release to validate the full flow without going public. Promote to Production once everything looks good.

---

## Phase 9 — Submit for Review

1. Resolve any policy warnings shown on the review screen
2. Click **Start rollout to Production** (or your chosen track)
3. Google reviews new apps — expect **1–3 business days** for the first submission

---

## Phase 10 — GitHub Actions CI/CD

Automate release builds by creating `.github/workflows/android-release.yml` in the repository root:

```yaml
name: Android Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: '20'

      - uses: actions/setup-java@v4
        with:
          java-version: '17'
          distribution: 'temurin'

      - name: Install dependencies
        working-directory: src/ThePrey
        run: npm ci

      - name: Build web assets
        working-directory: src/ThePrey
        run: npx ionic build --prod

      - name: Sync Capacitor
        working-directory: src/ThePrey
        run: npx cap sync android

      - name: Create key.properties
        working-directory: src/ThePrey/android
        run: |
          echo "storePassword=${{ secrets.KEYSTORE_PASSWORD }}" > key.properties
          echo "keyPassword=${{ secrets.KEY_PASSWORD }}" >> key.properties
          echo "keyAlias=theprey" >> key.properties
          echo "storeFile=keystore.jks" >> key.properties

      - name: Decode keystore
        working-directory: src/ThePrey/android
        run: echo "${{ secrets.KEYSTORE_BASE64 }}" | base64 --decode > keystore.jks

      - name: Build Release AAB
        working-directory: src/ThePrey/android
        run: ./gradlew bundleRelease

      - name: Upload AAB artifact
        uses: actions/upload-artifact@v4
        with:
          name: app-release.aab
          path: src/ThePrey/android/app/build/outputs/bundle/release/app-release.aab
```

### Required GitHub Secrets

Add these under **Settings → Secrets and variables → Actions**:

| Secret | How to get the value |
|---|---|
| `KEYSTORE_BASE64` | Run `certutil -encode theprey-release.jks keystore.b64` on Windows, copy the content |
| `KEYSTORE_PASSWORD` | The keystore password you chose in Phase 3 |
| `KEY_PASSWORD` | The key password you chose in Phase 3 |

Trigger a release build by pushing a git tag:

```powershell
git tag v1.0.0
git push --tags
```

---

## Version Bump Checklist (every future release)

1. Increment `versionCode` (integer, must always increase) in `android/app/build.gradle`
2. Update `versionName` (human-readable, e.g. `"1.1.0"`) in the same file
3. Run `npx ionic build --prod && npx cap sync android`
4. Run `.\gradlew bundleRelease`
5. Upload the new `.aab` in Play Console and create a new release

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `keytool` not found | Add JDK `bin/` directory to your PATH |
| Gradle sync fails in Android Studio | Let Android Studio download missing SDK components automatically |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` | Uninstall the debug build from the test device first |
| App rejected — missing privacy policy | Host a privacy policy page and add its URL under App content in Play Console |
| Location permissions flagged | Explain GPS usage clearly in the Data safety form |
| Build fails with `key.properties not found` | Confirm the file path in `build.gradle` matches where you placed the file |

---

## Critical Reminders

- **Never lose `theprey-release.jks`.** Without it you cannot publish updates — Google ties all updates to the original signing key and there is no recovery path.
- **Never commit `key.properties` or the `.jks` file** to git. Add both to `.gitignore`.
- Keep a backup of the keystore in a password manager or secure cloud storage.

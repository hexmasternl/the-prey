# The Prey — Automating Android Deployment with GitHub Actions

This guide builds a workflow that, on every version tag, builds a signed Android
App Bundle and **publishes it straight to Google Play** — no manual upload.

It assumes you're comfortable with GitHub Actions (jobs, steps, secrets,
triggers) but new to Android, app signing, and the Play Console. So the focus is
on the *app-specific* concepts and how they map into a workflow. The YAML
mechanics you already know are kept brief.

> Prerequisite: the manual flow in [`google-play-store.md`](./google-play-store.md)
> must have been done **once** — see [The first upload must be manual](#the-first-upload-must-be-manual) below for why.

---

## 1. The mental model (read this first)

Five concepts do all the work. If these click, the workflow is obvious.

### 1.1 What you're actually shipping: an AAB

You don't upload an app's source, and you don't upload an APK. You upload an
**Android App Bundle** (`.aab`) — a packaging format Google re-processes on
their servers to generate per-device APKs. For CI this is just "the build
artifact we publish." It's produced by the Gradle task `bundleRelease`.

The whole build chain is:

```
Ionic/Angular web build  →  Capacitor copies it into the native Android project  →  Gradle builds & signs the .aab
   (npx ionic build)          (npx cap sync android)                                  (./gradlew bundleRelease)
```

### 1.2 Signing — the part with no undo button

Every Android app is cryptographically signed with a **keystore** (a `.jks`
file). Google permanently ties your app's identity to the *first* key it sees.

- **Lose the keystore → you can never ship an update again.** There is no reset.
- The keystore is protected by two passwords (the store password and the key
  password) and contains a named key (the *alias*).

In CI we can't commit a binary secret file, so the standard trick is:
**base64-encode the keystore into a GitHub secret**, then decode it back to a
file inside the runner at build time. The two passwords and alias become
secrets too.

### 1.3 versionCode vs versionName — and why CI must set them

Two version fields live in `android/app/build.gradle`:

| Field | Audience | Rule |
|---|---|---|
| `versionCode` | Google's servers | An **integer that must strictly increase** on every upload. Play rejects an upload whose `versionCode` it has seen before. |
| `versionName` | Humans (shown in the store) | Any string, e.g. `"1.4.0"`. No uniqueness rule. |

This is the single most common reason a Play upload fails: a `versionCode` that
didn't go up. The robust fix in CI is to **not hardcode it** — feed it from the
build instead. `github.run_number` is perfect: it's an integer that increases
on every workflow run, automatically.

### 1.4 Tracks — Play's deployment rings

Play doesn't have "prod" and "staging"; it has **tracks**, which are release
rings:

```
internal  →  alpha (closed)  →  beta (open)  →  production
(fastest,     (small test       (wider test     (everyone;
 ~minutes)     group)            group)          review can take days)
```

Your workflow picks which track an upload lands in. **Default to `internal`** —
it's near-instant and reviewer-free, so it's where you validate that automation
works without risking the public listing.

### 1.5 The service account — CI's identity for Play

GitHub can't log in as *you*. It authenticates to the Play Developer API as a
**service account**: a robot Google identity with a JSON key. You create it in
Google Cloud, then grant it release rights inside the Play Console. That JSON
becomes one more GitHub secret.

---

## 2. One-time setup (outside the repo)

These steps happen in web consoles, once. The workflow can't do them for you.

### 2.1 Create the signing keystore (if you haven't)

If `theprey-release.jks` already exists from the manual guide, **reuse it** —
do not generate a new one. Otherwise, see Phase 3 of
[`google-play-store.md`](./google-play-store.md#phase-3--generate-a-signing-keystore).

### 2.2 Create the Play service account

1. **Google Cloud Console** → pick/create a project → **IAM & Admin →
   Service Accounts → Create service account**. No GCP roles are required.
2. Open the new account → **Keys → Add key → Create new key → JSON**. A `.json`
   file downloads. Treat it like a password.
3. **Play Console** → **Users and permissions → Invite new user** → paste the
   service account's email address.
4. Grant it app access to **The Prey** with the **Releases** permission
   (it does *not* need Admin). Save.

> Propagation can take a few minutes to ~24h before the API accepts the account.

### 2.3 The first upload must be manual

Google's API refuses to publish to a package that has **never** had an AAB
uploaded through the Play Console UI. So the very first release must go through
the manual flow (Phases 5–8 of the other guide). After that one upload exists,
every subsequent release can be fully automated by this workflow. Keep the
manual guide around purely for this bootstrap.

---

## 3. Make Gradle accept versions from CI

By default `build.gradle` hardcodes the version. Change `defaultConfig` so it
reads the values CI passes in (falling back to sane defaults for local builds):

```groovy
// src/ThePrey/android/app/build.gradle
android {
    defaultConfig {
        // ...
        versionCode (project.findProperty("versionCode") ?: 1).toInteger()
        versionName project.findProperty("versionName") ?: "1.0.0"
    }
}
```

The workflow will pass these as Gradle project properties via the
`ORG_GRADLE_PROJECT_*` environment-variable convention — no command-line
juggling needed.

---

## 4. The GitHub secrets

Add these under **Settings → Secrets and variables → Actions**:

| Secret | What it is | How to produce the value |
|---|---|---|
| `KEYSTORE_BASE64` | The `.jks` keystore, base64-encoded | Windows: `certutil -encode theprey-release.jks keystore.b64` then copy the file's contents. (Or `[Convert]::ToBase64String([IO.File]::ReadAllBytes("theprey-release.jks"))`.) |
| `KEYSTORE_PASSWORD` | Keystore (store) password | Chosen when the keystore was created |
| `KEY_PASSWORD` | Key (alias) password | Chosen when the keystore was created |
| `PLAY_SERVICE_ACCOUNT_JSON` | Full contents of the service-account JSON | The file downloaded in step 2.2 — paste the whole JSON |

> The key alias (`theprey`) isn't secret, so the workflow can hardcode it.

---

## 5. The workflow

Create `.github/workflows/android-release.yml`. Read §6 for what each
app-specific step does.

```yaml
name: Android Release

on:
  push:
    tags: ['v*']            # e.g. push tag v1.4.0 to ship
  workflow_dispatch:        # or run manually and choose a track
    inputs:
      track:
        description: 'Play track to release to'
        type: choice
        options: [internal, alpha, beta, production]
        default: internal

permissions:
  contents: read

concurrency:
  group: android-release-${{ github.ref }}
  cancel-in-progress: false   # never cancel a half-finished publish

jobs:
  build-and-publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0      # GitVersion needs full history

      # ---- version numbers -------------------------------------------------
      - name: Setup GitVersion
        uses: gittools/actions/gitversion/setup@v4.5.0
        with:
          versionSpec: '6.4.x'

      - name: Execute GitVersion
        id: gitversion
        uses: gittools/actions/gitversion/execute@v4.5.0

      # ---- toolchains ------------------------------------------------------
      - uses: actions/setup-node@v4
        with:
          node-version: '20'

      - uses: actions/setup-java@v4
        with:
          java-version: '17'        # Capacitor's officially supported JDK
          distribution: 'temurin'

      # ---- build the web app and copy it into the native project ----------
      - name: Install dependencies
        working-directory: src/ThePrey
        run: npm ci

      - name: Build web assets
        working-directory: src/ThePrey
        run: npx ionic build --prod

      - name: Sync Capacitor
        working-directory: src/ThePrey
        run: npx cap sync android

      # ---- restore the signing material into the runner -------------------
      - name: Decode keystore
        working-directory: src/ThePrey/android
        run: echo "${{ secrets.KEYSTORE_BASE64 }}" | base64 --decode > keystore.jks

      - name: Create key.properties
        working-directory: src/ThePrey/android
        run: |
          echo "storePassword=${{ secrets.KEYSTORE_PASSWORD }}" > key.properties
          echo "keyPassword=${{ secrets.KEY_PASSWORD }}" >> key.properties
          echo "keyAlias=theprey" >> key.properties
          echo "storeFile=keystore.jks" >> key.properties

      # ---- build the signed bundle ----------------------------------------
      - name: Build Release AAB
        working-directory: src/ThePrey/android
        env:
          ORG_GRADLE_PROJECT_versionCode: ${{ github.run_number }}
          ORG_GRADLE_PROJECT_versionName: ${{ steps.gitversion.outputs.semVer }}
        run: ./gradlew bundleRelease

      # ---- publish to Google Play -----------------------------------------
      - name: Publish to Google Play
        uses: r0adkll/upload-google-play@v1
        with:
          serviceAccountJsonPlainText: ${{ secrets.PLAY_SERVICE_ACCOUNT_JSON }}
          packageName: nl.hexmaster.theprey
          releaseFiles: src/ThePrey/android/app/build/outputs/bundle/release/app-release.aab
          track: ${{ github.event.inputs.track || 'internal' }}
          status: completed
```

---

## 6. What each app-specific step is doing

Most of this workflow is ordinary CI. These are the steps that exist *only*
because it's a mobile app — the parts you said you don't know yet.

**Setup Java (JDK 17).** The Android build runs on Gradle, which runs on Java.
17 is what Capacitor officially supports; the runner ships several JDKs and this
selects one. (This is the CI equivalent of the `JAVA_HOME` issue you hit
locally — here `setup-java` configures it correctly for you.)

**Build web assets + Sync Capacitor.** The Prey is a web app inside a native
shell. `ionic build` produces the web bundle; `cap sync android` copies that
bundle into `android/` and updates native plugins. After this, the `android/`
folder is a complete, buildable native project.

**Decode keystore + Create key.properties.** Reconstructs the two pieces Gradle
needs to *sign* the build: the keystore file (decoded from the base64 secret)
and a `key.properties` file telling Gradle the passwords, alias, and keystore
path. `storeFile=keystore.jks` is relative to `android/`, which is where we
wrote both files. The signing config you added in the manual guide
(`google-play-store.md` Phase 4) reads this file. Nothing is committed; it all
lives only in the runner.

**Build Release AAB.** `bundleRelease` is the Gradle task that compiles, signs,
and packages the `.aab`. The two `ORG_GRADLE_PROJECT_*` env vars feed
`versionCode` and `versionName` into the `defaultConfig` block from §3.
`github.run_number` guarantees a strictly increasing `versionCode`; GitVersion's
`semVer` gives a readable `versionName`.

**Publish to Google Play.** The `r0adkll/upload-google-play` action calls the
Play Developer API, authenticating with the service-account JSON. It uploads the
`.aab` to the chosen `track`. `status: completed` means "release it on that
track immediately"; alternatives are `draft` (uploads but waits for you to click
release in the Console) and `inProgress` (staged rollout to a percentage).

---

## 7. How you'll actually use it

**Ship a release** — tag and push:

```powershell
git tag v1.4.0
git push origin v1.4.0
```

The tag push triggers the workflow → builds → publishes to `internal`.

**Promote / pick a track** — run it from the Actions tab via
*Run workflow* and choose `alpha`, `beta`, or `production`.

**Why `versionName` comes from the tag, sort of:** GitVersion derives `semVer`
from your git history and tags, so a `v1.4.0` tag yields a `1.4.0`-ish version
name automatically — consistent with how your server workflows
(`games.yml`, etc.) already version things.

---

## 8. Recommended hardening (once the basics work)

- **Start with `status: draft`** for your first few automated runs. The AAB
  uploads, but you click the final "release" button in the Console — a safety
  net while you trust the pipeline.
- **Staged rollout to production.** When you do automate production, use a
  partial rollout (e.g. 20%) instead of `completed`, so a bad build reaches only
  a fraction of users. The action supports `userFraction` with
  `status: inProgress`.
- **Use a GitHub Environment** (e.g. `play-production`) with a required
  reviewer on the production track, so a human approves before public releases.
- **Don't run on every push.** Tag-triggered (as above) keeps random commits off
  the Play Store.

---

## 9. Troubleshooting

| Symptom | Cause / Fix |
|---|---|
| `APK specifies a version code that has already been used` | `versionCode` didn't increase. Confirm `build.gradle` reads `project.findProperty("versionCode")` (§3) and that `ORG_GRADLE_PROJECT_versionCode` is set. |
| `The caller does not have permission` (Play API) | Service account not granted **Releases** access in Play Console, or permissions haven't propagated yet. Recheck step 2.4 and wait. |
| `Package not found: nl.hexmaster.theprey` | The first manual upload (§2.3) hasn't happened. The API can't see a package that's never been published via the Console. |
| `keystore.jks` / signing errors | The base64 secret is malformed (extra whitespace/newlines from copy-paste). Re-encode and re-paste `KEYSTORE_BASE64`. |
| Gradle "Unsupported class file major version" | JDK mismatch. Pin `java-version: '17'` in `setup-java`. |
| Upload succeeds but nothing appears live | You used `status: draft`, or uploaded to a track you're not testing. Check the right track in the Console. |

---

## 10. Critical reminders

- **Never lose the keystore.** It is irreplaceable; back it up in a password
  manager or secure vault.
- **Never commit** `theprey-release.jks`, `key.properties`, or the service
  account JSON. They live only as GitHub secrets and as transient files inside
  the runner.
- **`versionCode` only ever goes up.** Never reset or reuse it.

---
name: the-prey-play-store
description: Google Play compliance and release rules for The Prey's Android app (nl.hexmaster.theprey) — the prominent location disclosure and where it is implemented, manifest permissions and foreground-service declarations with their justification, the Data safety form derived from what the app really collects and sends, the privacy and account-removal pages, store listing and "What's new" texts in English and Dutch, the internal-track release workflow, versionCode rules, promotion to other tracks and the app version gate. Use whenever you add or change an Android permission, the manifest, the tracking foreground service, the consent/disclosure UI, anything the app collects or sends (new API fields, SDKs, analytics, third-party calls), the privacy page, android-release.yml, versioning, or when preparing a Play Console declaration, Data safety answer, store listing or release.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Google Play Store

The app is `nl.hexmaster.theprey`, the id of the previously published app, so every release updates the existing Play listing. Play policy is enforced on **what the code does**, so any change to permissions, location behaviour or collected data must land together with the disclosure, the privacy page and the Play Console answers.

Items marked **[Play Console]** are human actions in the Play Console. You cannot do them; write the text, then tell the user exactly what to enter where.

## Never

- Change `applicationId` / `namespace` (`app/src/app/build.gradle.kts`). A new id is a new app with no users.
- Commit `app/src/key.properties`, a `*.jks` / `*.keystore`, or the Play service-account JSON. They are git-ignored; CI restores them from secrets.
- Request the system location prompt before the disclosure is accepted, or request `ACCESS_BACKGROUND_LOCATION` at runtime (the player grants "Allow all the time" on the system settings page).
- Collect location outside an active game, or start the tracking service from the background.
- Ship a `versionCode` that is not higher than the last one in Play, or a pre-release version.
- Build a new bundle to move a release to another track. Promotion reuses the internal-track build.
- Change disclosure, privacy page or Play Console text in one language only (`the-prey-multilingual`).

## Where compliance lives

| Concern | File |
|---|---|
| Permissions, service, receivers | `app/src/app/src/main/AndroidManifest.xml` |
| Disclosure text (one copy, shared) | `ui/consent/LocationDisclosure.kt`, strings `location_consent_*` in `res/values/strings.xml` + `res/values-nl/strings.xml` |
| Disclosure dialog / permission flow | `ui/consent/LocationDisclosureDialog.kt`, `LocationConsentHost.kt`, `LocationConsentViewModel.kt`; flag in `data/location/LocationConsentStore.kt` |
| Permission levels | `data/location/LocationAccess.kt` (`Missing`, `ApproximateOnly`, `ForegroundOnly`, `Granted`) |
| Play gating on "Allow all the time" | `ui/mainmenu/PlayAction.kt`, `MainMenuScreen.kt` (opens app settings) |
| Tracking foreground service | `tracking/LocationTrackingService.kt` (type `location`), `TrackingController.kt` (5 s ticks, stops at game end + 2 min grace), `TrackingStarter.kt`, `TrackingNotifications.kt`, `TrackingReceivers.kt` |
| Play Console declaration + Data safety text | `docs/play-store/location-disclosure.md` |
| Privacy statement | `website/src/content/{en,nl}/privacy.md` (section *In-Game Location Tracking* / *Locatievolging Tijdens een Spel*) |
| Account deletion page (Play's deletion URL) | `website/src/content/{en,nl}/account-removal.md` → `https://theprey.nl/en/account-removal/` |
| App Links verification | `website/src/static/.well-known/assetlinks.json` |
| Release pipeline | `.github/workflows/android-release.yml`, `.github/workflows/version.yml`, `.github/actions/create-release/` |
| Version gate | Server `CheckAppVersionQueryHandler` (`POST /games/version-checker`, key `Games:MinimumAppVersion`); client and promote flow in OpenSpec `app-demand-updates` |

The disclosure strings, the privacy page section and `docs/play-store/location-disclosure.md` are one statement in three places. **Change all three together** (and the Data safety answers in the Play Console).

## Prominent disclosure (User Data / Location policy)

Implemented by the OpenSpec change `app-location-foreground-service-warning`:

- A non-dismissible dialog, shown **before** any system location prompt: once by itself on the main menu (per device), and again before starting or joining a game (`LocalLocationConsent.current.requireLocation { … }` — wrap every start/join action in it). The Settings screen shows the same dialog when *Grant location access* would show the prompt, and keeps the `LocationDisclosure` as a collapsible panel ("Why The Prey uses your location", collapsed by default) below that button; a collapsed panel alone is not a prominent disclosure. After a precise grant without background (API 29+), the flow continues with an "Allow all the time" step that opens the app's settings page; `ACCESS_BACKGROUND_LOCATION` is never requested at runtime.
- It states: precise location, only during an active game; continues in the background / app closed / screen off; foreground service with a persistent notification; stops when the game ends or the player leaves; why (show players to the Hunter, playfield boundary check); a link to the privacy page (`privacyPolicyUrl()` picks `/en/` or `/nl/`).
- **Continue** → fine + coarse prompt, then `POST_NOTIFICATIONS` on Android 13+. **Not now** → menus keep working, no prompt.
- Only a fine-location grant counts; to play, `LocationAccess.Granted` is required (on API 29+ that means "Allow all the time").

If gameplay starts using location in a new way (new purpose, new recipient, other timing), the disclosure text must say so before it ships.

## Permissions and their justification

| Permission | Why | Play requirement |
|---|---|---|
| `INTERNET` | API and realtime socket | — |
| `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION` | Gameplay position (fine); coarse is enough for the main-menu readout, which never leaves the device | Location permission declaration **[Play Console]** |
| `ACCESS_BACKGROUND_LOCATION` | Reporting continues with the phone in a pocket / screen off; required to play | **Background location declaration + review video [Play Console]** |
| `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION` | `LocationTrackingService`, `foregroundServiceType="location"`, started only from the foreground via `TrackingStarter` | **Foreground service declaration (location) + video [Play Console]** |
| `POST_NOTIFICATIONS` | The persistent tracking notification on Android 13+ | — |
| `WAKE_LOCK` | Timed wake lock while tracking | — |
| `RECEIVE_BOOT_COMPLETED` | `TrackingBootReceiver` posts a "tap to resume tracking" prompt after reboot / app update; it does not restart tracking itself | — |

Adding a permission means: a manifest comment explaining it, a row here, the justification text in `docs/play-store/` (EN + NL), and a note to the user about any Play Console declaration it triggers.

## Data safety form **[Play Console]**

Derive answers from the code, not from memory. What the app currently collects and sends (all to our own backend over HTTPS, nothing sold, no ads, no analytics or crash SDK):

| Play data type | Source in code | Purpose | Notes |
|---|---|---|---|
| Location → Precise + Approximate | `RecordLocationRequest` (lat, lon, accuracy, time) every 5 s during a game | App functionality | Shown to other players of the game; deleted 48 h after the game ends |
| Personal info → Name | `CreateUserRequest.firstName/lastName`, display name, callsign (`UpdateUserSettingsRequest`) | Account management, App functionality | Callsign/display name visible to other players |
| Personal info → Email address | `CreateUserRequest.emailAddress` (from Auth0) | Account management | |
| Personal info → User IDs | Auth0 `sub`, user id | Account management | |
| App activity → Other user-generated content / in-app actions | Playfields (`CreatePlayFieldRequest`), games, tags, results, `preferredLanguage` | App functionality | |
| App info and performance (server-side) | Request telemetry in Application Insights; app version for the version gate | Analytics/diagnostics as described in the privacy page | Decide with the user whether to declare |

Other answers: encrypted in transit **yes**; users can request deletion **yes** (account and data via `account-removals@theprey.nl` / the account-removal page, which is the deletion URL Play asks for; privacy questions `privacy@theprey.nl`); location is optional for the menus but required to play. Map tiles load from `https://tiles.openfreemap.org` (`MAP_STYLE_URL` in `ui/playfields/area/PlayFieldMap.kt`); no user data is sent beyond the normal tile request. Any new SDK, endpoint field or third-party call → update this table, the privacy page and the Play Console form in the same change.

## Store listing and release notes (EN + NL)

- Store listing texts (title, short and full description) are **not kept in the repo**. When asked to write or change them, write both languages following `the-prey-multilingual` (informal Dutch, glossary terms) and the design-system voice, keep Play limits (title 30, short description 80, full description 4000 characters), and hand them to the user for **[Play Console]** → *Main store listing* (en-US and nl-NL).
- "What's new" is generated by `android-release.yml`: Copilot turns the `app/src` commits since the last successful run into `whatsnew/whatsnew-en-US` and `whatsnew/whatsnew-nl-NL` (max 500 characters each, cut at a line break). On failure it falls back to commit subjects (Dutch prefixed with "Verbeteringen en bugfixes."). So **write player-facing commit subjects** for app changes — they become release notes.
- Screenshots and the feature graphic are uploaded in the Play Console by hand.

## Release flow

1. PRs and pushes to `main` touching `app/src/**` run `./gradlew lintDebug testDebugUnitTest`.
2. On `main` the `publish` job: `semVer` from `version.yml` (fails on empty or pre-release), `versionCode = major * 1000000 + minor * 1000 + patch + VERSION_CODE_OFFSET` (`1.1.8` → `1001008`; offset is the workflow `env` value, currently `"0"`), `versionName = semVer`, restores the upload keystore from `KEYSTORE_BASE64` into `key.properties`, builds `bundleRelease`, uploads to the **internal** track as `completed` (`r0adkll/upload-google-play@v1`), then creates GitHub release `v<semVer>`.
3. **Promotion** to closed/open/production is a **[Play Console]** action on the existing release ("Promote release"); never a new build. `app-demand-updates` plans `.github/workflows/android-promote.yml` (fastlane `--track internal --track_promote_to production`) — it does not exist yet; check before referring to it.
4. Forcing an update: raise `Games:MinimumAppVersion` only after the new version is **live in production**, never on the internal publish. Today `server/infrastructure/games/main.bicep` rewrites it to `1.0.14` on every Games deploy (removal planned in `app-demand-updates` task 1.3), and the Android client gate is not implemented yet. See `the-prey-azure-infra`.
5. Signing: CI signs with the **upload key**; Google re-signs with the Play App Signing key. App Links (`assetlinks.json`) and the Auth0 `https` callback need the **Play App Signing** SHA-256 too **[Play Console → App integrity]** — add it to `assetlinks.json` and to Auth0 Device Settings.

## Verify

```powershell
# from app/src
./gradlew lintDebug testDebugUnitTest   # MissingTranslation, permission lint, consent/tracking tests
./gradlew bundleRelease -PversionCode=1001008 -PversionName=1.1.8   # unsigned locally unless key.properties exists

# merged manifest: every permission and service type you expect, nothing extra (e.g. from a new library)
Select-String -Path app/build/intermediates/merged_manifest/release/processReleaseMainManifest/AndroidManifest.xml -Pattern "uses-permission|foregroundServiceType"
```

```bash
# disclosure keys exist in both languages
grep -o 'name="location_consent_[a-z_]*"' app/src/app/src/main/res/values/strings.xml | sort > /tmp/en.txt
grep -o 'name="location_consent_[a-z_]*"' app/src/app/src/main/res/values-nl/strings.xml | sort | diff /tmp/en.txt -
# secrets never tracked
git ls-files | grep -Ei 'key\.properties|\.jks$|\.keystore$'   # must print nothing
```

Then compare the disclosure strings with `docs/play-store/location-disclosure.md` and the privacy page section by hand, and build the site (`hugo` from `website/src`).

## Checklist

1. `applicationId` unchanged; no keystore, `key.properties` or service-account JSON committed.
2. New/changed permission: manifest comment, table above, declaration text in `docs/play-store/` (EN + NL), Play Console action named to the user.
3. Location behaviour change: disclosure strings, privacy page (EN + NL) and `docs/play-store/location-disclosure.md` updated together; system prompt still only after the disclosure.
4. New collected/sent data or third party: Data safety table, privacy page and Play Console form updated.
5. Store/listing texts in English and Dutch (`the-prey-multilingual`); app commit subjects readable as release notes.
6. Release: version from `version.yml`, no pre-release, `versionCode` increases; promotion done in the Play Console, not by rebuilding.
7. Minimum app version raised only after the version is live in production.
8. List every **[Play Console]** step the user still has to do in your summary.

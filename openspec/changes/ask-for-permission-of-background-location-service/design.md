## Context

Both clients already run a background location service that broadcasts the device position for the full duration of a game, and both request the OS location permission implicitly at tracking start:

- **MAUI** — `AndroidBackgroundExecutionHost.EnsurePermissionsAsync()` calls `Permissions.RequestAsync<Permissions.LocationWhenInUse>()` then `<Permissions.LocationAlways>()` (and `POST_NOTIFICATIONS` on API 33+) the moment `StartAsync` runs. iOS declares usage strings in `Info.plist`. There is no in-app explanation shown first.
- **Ionic** — `GameLocationService.start()` calls `BackgroundGeolocation.addWatcher({ requestPermissions: true, ... })`, which makes the plugin request the OS permission directly. Callers are `game-lobby.page.ts`, `game-hunter.page.ts`, and the prey page.

Google Play's **Prominent Disclosure & Consent** policy requires that, before the runtime permission request, the app show a prominent disclosure describing background location collection and its purpose, gated by an affirmative user action. Neither app does this today. This change inserts a consent gate ahead of the existing permission requests; it does not change how location is captured or reported.

## Goals / Non-Goals

**Goals:**
- Show a prominent, purpose-explaining disclosure before any OS location prompt, in both clients, with identical intent.
- Only request OS permission / start tracking after explicit consent; decline degrades gracefully (no tracking, game still playable).
- Remember consent so returning players are not re-prompted every game, re-disclosing only when consent is missing or the OS permission was revoked.
- Localize (EN + NL) and style the disclosure through each app's single source of truth.

**Non-Goals:**
- No change to location capture, cadence, reporting endpoints, or the foreground-service/background behaviour.
- No backend, API, or realtime-protocol changes.
- Not a full privacy-policy or settings-screen redesign; only the pre-permission disclosure gate.
- No change to the head-start / penalty flows.

## Decisions

### Decision: Insert a consent gate immediately before the OS permission request, not at app launch
The disclosure is shown at the point tracking is about to start (game becomes InProgress), immediately before the OS prompt — contextual and policy-aligned (disclosure tied to the feature that needs it).

- **Why**: Google Play wants the disclosure adjacent to the permission request and its context, not a generic launch splash. It also avoids prompting users who never start a game.
- **Alternative considered**: A one-time onboarding screen at first launch. Rejected — decontextualized, and the OS permission would still fire later without an adjacent disclosure.

### Decision: MAUI — a dedicated consent surface awaited before `EnsurePermissionsAsync`
Add a consent step the tracker coordinator awaits before Android/iOS permission requests. Model it like the app's existing dialog services (e.g. `IConfirmationDialog`) so it is injectable and unit-testable: a `ILocationConsentGate` (or similar) that returns granted/declined, backed by a modal page or `DisplayAlert`-style surface. Persist the consent flag with `Preferences`/secure storage. `AndroidBackgroundExecutionHost.EnsurePermissionsAsync()` (and the iOS host) run only after the gate returns consent.

- **Why**: Keeps platform hosts free of UI, matches the existing dialog-service pattern, and stays testable behind an interface.
- **Alternative considered**: Put the disclosure inside `AndroidBackgroundExecutionHost`. Rejected — that class is a platform adapter excluded from the test build and should not own UI or cross-platform consent state.

### Decision: Ionic — an awaited consent modal/alert before `addWatcher`
`GameLocationService.start()` awaits a consent step before calling `addWatcher`. When consent is absent, either show the disclosure (Ionic `ModalController`/`AlertController`) and proceed on accept, or short-circuit `start()` without beginning tracking on decline. Persist consent in `@capacitor/preferences` (already a dependency). On native, keep `requestPermissions: true` on the watcher so the OS prompt still follows — but only after consent. Callers already `await locationService.start(...)`, so the gate slots in without changing call sites.

- **Why**: One choke point (`start`) covers all callers (lobby, hunter, prey) with no duplication.
- **Alternative considered**: Gate in each page before calling `start`. Rejected — three call sites to keep in sync; the service is the single source of truth for tracking.

### Decision: Remember consent, re-disclose only when needed
Persist a "disclosure accepted" flag per install. Show the disclosure when the flag is unset, or when the OS permission is found not-granted at start (covering revocation in system settings). This satisfies the policy (disclosure before a fresh permission request) without nagging returning players.

- **Why**: Policy requires disclosure before requesting permission; once granted and remembered, re-requesting is unnecessary. Revocation re-triggers a genuine new request, so re-disclosure is appropriate then.

## Risks / Trade-offs

- **[Two independent implementations can drift]** → A single shared capability spec (`background-location-consent`) defines identical required behaviour for both clients; tasks implement to the same scenarios.
- **[Ionic plugin requests permission itself via `requestPermissions: true`]** → Ensure the disclosure is awaited *before* `addWatcher` is called; do not rely on the plugin's own prompt ordering. If finer control is needed, request permission explicitly after consent and pass `requestPermissions: false`.
- **[Persisted consent could mask a later OS revocation]** → Re-check the OS permission state at start and re-disclose when it is not granted, rather than trusting the stored flag alone.
- **[Decline could strand the player on an empty map]** → The existing `gpsError`/foreground-only degradation already surfaces missing location; declining consent routes into that same graceful state instead of a crash or prompt loop.
- **[Localization completeness]** → Add EN and NL copy in the same change for both apps; both apps switch language at runtime, so a missing key would be immediately visible.

## Open Questions

- Should declining the disclosure be surfaced with a one-tap way to reconsider (e.g. a HUD affordance), or is re-disclosure on the next game-start attempt sufficient? (Leaning: next-attempt is enough for policy; a HUD affordance can be a follow-up.)
- iOS specifics: confirm the disclosure copy and that `Info.plist` `NSLocation*` usage strings align with the in-app disclosure wording.

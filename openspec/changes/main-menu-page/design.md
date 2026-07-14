## Context

The MAUI app (`HexMaster.ThePrey.Maui.App`) already has a working startup flow (`WelcomePage` → `WelcomeViewModel.BootstrapAsync`) built by the sibling `maui-app-front-page` change. That flow resolves a session through `ISessionService.TryEstablishSessionAsync()` — a `SessionResult` of `ActiveGame` / `NoActiveGame` / `Unauthenticated` — and today routes each outcome to a *different* page: `game`, `home` (a bare stub), or `login`.

The tactical theme is centralized: `Resources/Styles/Colors.xaml` holds the `Tp*` palette (`TpBgVoid #0c0e0c`, `TpBgBase #181b17`, `TpSignal #64ff00`, `TpHunter #ff2f1f`, `TpText`, `TpTextSoft`, `TpTextGhost`, `TpSignalDeep`, …) and `Resources/Styles/Styles.xaml` holds the styles (`TacticalTitle`, `HudLabel`, `StatusLabel`, `PrimaryButton`, `CornerBracket`, `HudPanel`, `SignalGlow`). The `maui-styling-expert` rule is strict: pages carry **no** inline visual properties — every color/size/border/glow comes from a named style or color key.

The interactive Auth0 login logic currently lives inside `LoginViewModel` (PKCE build → `WebAuthenticator` → `IAuth0TokenClient.ExchangeCodeAsync` → store refresh token → re-bootstrap). Secure token storage is `ITokenStore` (`SecureStorageTokenStore`), which already exposes `ClearRefreshToken()`.

The `maui-splash-screen` change brings a larger-than-viewport tactical map raster into `Resources/Images/the-prey-background.png` and a `SplashMapBackground` style — the same asset this menu pans.

## Goals / Non-Goals

**Goals:**
- Turn the `HomePage` stub into the app's real main-menu hub that reflects sign-in and active-game state through its buttons.
- A slowly panning, dimmed tactical-map background that reads as "alive" without harming legibility or input.
- Correct, testable button visibility/enablement state driven by session state, with the seven actions wired.
- Reuse the existing session/auth building blocks (`ISessionService`, `ITokenStore`, the PKCE login flow) rather than duplicating them.

**Non-Goals:**
- Building the Start Game, Playfields, and Settings screens — they stay navigation stubs this menu routes to.
- Auth0 server-side logout / session-cookie clearing (only the local refresh token is cleared for Log Out).
- Live gameplay, playfield editing, settings persistence.
- A perfectly smooth infinite parallax; a simple looping translate is sufficient.

## Decisions

### D1: Main menu becomes the universal post-boot destination

`WelcomeViewModel.BootstrapAsync` is changed to navigate to `home` (the main menu) for **all** outcomes — `ActiveGame`, `NoActiveGame`, and `Unauthenticated` — instead of forking to `game` / `home` / `login`. The menu itself decides what to show. This matches the requirement that the menu shows Resume Game for active games and Log In when signed out, rather than the app auto-routing past the menu.
- *Alternative considered:* keep welcome routing to three pages and only rebuild `home`. Rejected — it can never show "Resume Game on the menu" for active-game users (they'd be sent straight into the game) and duplicates the login entry point.

### D2: `MainMenuViewModel` owns menu state; it re-uses `ISessionService`

A new `MainMenuViewModel` exposes observable state — `IsSignedIn`, `HasActiveGame`, `IsBusy` — plus computed visibility/enablement flags (`ShowLogIn`, `ShowResume`, `ShowStart`, `CanUseSignedInActions`) and one `Command` per button. On appearing it calls `ISessionService.TryEstablishSessionAsync()` and maps the `SessionResult` to its state: `ActiveGame` → signed-in + has-game; `NoActiveGame` → signed-in + no-game; `Unauthenticated` → signed-out. The XAML binds `IsVisible` / `IsEnabled` to these flags.
- *Rationale:* `ISessionService` already composes token-refresh + active-game check into exactly the tri-state the menu needs, and it is the unit-tested seam. The view model stays thin and mockable.
- *Alternative considered:* a separate lightweight "is there a token?" check. Rejected — it can't tell active-game from no-game, and would drift from the bootstrap's own logic.

### D3: Extract the interactive login into a shared service

The PKCE authorize-URL build → `WebAuthenticator` → code exchange → store-refresh-token sequence currently embedded in `LoginViewModel` is extracted into an `IInteractiveLoginService.LoginAsync()` returning a success/cancelled/failed result. Both `LoginViewModel` (the standalone login page, kept for the sibling change) and `MainMenuViewModel` (the Log In button) call it. On success the menu re-runs its state evaluation in place — no navigation.
- *Alternative considered:* have the menu's Log In button navigate to the existing `LoginPage`. Rejected — the requirement is that login happens from the menu and the menu updates in place; a full page hop is heavier and loses the "stay on the menu" behavior. (If the team prefers the page hop, it is a smaller variant — see Open Questions.)

### D4: Panning background via a looping `TranslateTo` animation on an oversized `Image`

The background is an `Image` (`Aspect=AspectFill`) sized larger than the viewport inside a clipped `Grid` cell, with `InputTransparent=True` so it never steals taps. A code-behind (or attached-behavior) loop drives a slow, continuous pan using `TranslateTo` back and forth (or a ping-pong across the overscan), started `OnAppearing` and stopped `OnDisappearing` to avoid a runaway animation off-screen. Dimming is a style-applied overlay/opacity, not an inline literal.
- *Rationale:* `TranslateTo` is the built-in, dependency-free MAUI animation primitive; a simple ping-pong reads as a slow tactical drift and is cheap. The stop-on-disappear guard prevents battery/CPU waste.
- *Alternative considered:* a custom `SKCanvasView`/parallax or a `Microsoft.Maui.Graphics` drawable. Rejected as over-engineered for a background drift; revisit only if a richer parallax is wanted.
- *Note:* animation start/stop is presentation glue in code-behind; the visual literals (size, opacity, dimming) stay in `Styles.xaml`.

### D5: Two new button styles in the central `Styles.xaml`

Add `HunterButton` (filled `TpHunter` background, void text — for Resume Game) and `OutlineButton` (transparent/void background, `TpSignal` border via `BorderColor`/`BorderWidth`, signal-green text — for Playfields/Settings/Log Out/Exit), each with a `Disabled` visual state (dimmed to `TpTextGhost` / `TpSignalDeep`) mirroring the existing `PrimaryButton`. The existing `PrimaryButton` (filled green) serves Log In and Start Game. No inline styling on the page.

### D6: Exit uses `Application.Current.Quit()`, best-effort per platform

Exit calls `Application.Current?.Quit()`. This is well-behaved on Android/Windows/Mac; iOS discourages programmatic termination, so on iOS the Exit button is hidden (or a no-op) per the platform guidance. This keeps the button honest rather than shipping a control that silently does nothing.

### D7: GPS readout via MAUI `Geolocation`, decorative and non-blocking

The top-right GPS line reads the device position through `Geolocation.Default` (request permission, then `GetLastKnownLocationAsync()` falling back to a low-accuracy `GetLocationAsync`). It is **decorative flavor**, not gameplay data, so it is coarse (whole degrees, e.g. `052° N // 004° E`) and never blocks the menu: the fetch runs off the UI thread on appearing, and any denial/timeout/exception yields the placeholder `---° N // ---° E`. Formatting maps latitude sign→`N`/`S` and longitude sign→`E`/`W`, zero-padded to three digits.
- *Rationale:* whole-degree precision matches the `xxx°` mock and avoids leaking a precise home location into an always-on HUD; a degree is ~111 km, plenty for atmosphere.
- *Platform:* Android needs `ACCESS_COARSE_LOCATION` (fine optional); iOS needs `NSLocationWhenInUseUsageDescription` in `Info.plist`. Coarse permission is sufficient for a whole-degree readout.
- *Alternative considered:* a live-updating `GeolocationListener`. Rejected — an always-running GPS listener on the menu wastes battery for a decorative readout; a single fix on appear is enough.

### D8: App version from `AppInfo.Current.VersionString`

The field-manual line uses `AppInfo.Current.VersionString` (the platform-reported app version), formatted as `OPERATIONAL FIELD MANUAL — V {version}`. Exposed as a view-model property so the page binds it and carries no literal.

### D9: Header/hero/footer layout via a three-row `Grid`; slogan via styled `Span`s

The content overlay is a `Grid` with three rows: top header (title top-left, telemetry stack top-right), a star-sized middle row holding the left-aligned, vertically-centered slogan + tagline, and a bottom row holding the button stack. The two-line slogan uses `FormattedString` with `Span`s; the signal-green words carry a `SloganWordSignal` span style and the regular words a `SloganWord` span style, so the per-word color comes from central styles rather than inline literals (satisfying the no-inline-visual-properties rule). New styles land in `Styles.xaml`: `HudReadout` (small, dim, right-aligned PT Mono for the GPS + version lines), `SloganLine` (large left-aligned display), and `MenuTagline` (dim left-aligned PT Mono).
- *Note:* the earlier "title at the top" requirement is preserved by placing "The Prey" in the top-left of the header, balancing the top-right telemetry. If the team wants the slogan to stand alone without the title, that is a small markup change — see Open Questions.

### D10: Log Out clears local session only

Log Out calls `ITokenStore.ClearRefreshToken()`, discards the in-memory access token, sets `IsSignedIn=false`, and re-evaluates menu state — no navigation, no Auth0 server logout. The access token lives only in `ISessionService`/`MainMenuViewModel` for the session, so dropping the reference is sufficient.
- *Alternative considered:* hit the Auth0 `/v2/logout` endpoint to kill the SSO cookie. Deferred — not required by the spec and adds a browser round-trip; can be added later if account-switching needs a clean SSO state.

## Risks / Trade-offs

- **Changing welcome routing affects the sibling change's specs.** The (unarchived) `maui-welcome-screen` requirements say active-game→game and unauthenticated→login. → Adjust `WelcomeViewModel` here and reconcile the two changes at archive time; no archived spec changes, so no delta is owed now. Call it out in the PR.
- **Login logic extraction could regress the standalone login page.** → Extract behind `IInteractiveLoginService` with unit tests; `LoginViewModel` becomes a thin caller so both entry points share one tested path.
- **Panning animation running off-screen wastes battery.** → Start on appear, stop on disappear; keep the translate slow and bounded to the overscan.
- **Background image intercepting taps or letterboxing.** → `InputTransparent=True` on the image and `AspectFill` in a clipped cell; verify buttons receive taps and the map covers phone + tablet with no bars.
- **Style rule violations (inline colors/sizes on the new page).** → All treatment in `Colors.xaml`/`Styles.xaml`; review the page for literals before done (mirrors the splash-screen rule).
- **iOS Exit is a non-standard pattern.** → Hide Exit on iOS per D6 rather than ship a dead button.
- **Missing/oversized map raster.** → Reuse the `maui-splash-screen` asset with a sane `MauiImage` `BaseSize`; if that change hasn't landed, add the raster as part of this change's asset task.

## Migration Plan

Additive to the existing app. Steps: (1) add the button, background, readout, slogan, and tagline styles + keys; (2) extract `IInteractiveLoginService`; (3) add location permission entries (Android manifest, iOS `Info.plist`); (4) build `MainMenuViewModel` (session state + GPS readout + version) + rebuild `HomePage`; (5) repoint `WelcomeViewModel` routing to `home`; (6) register the new service/view model in `MauiProgram`; (7) ensure the map raster + `home` route exist. Rollback is reverting the `HomePage`/`WelcomeViewModel` edits — the stub and three-way routing return with no data impact.

## Open Questions

- Should Log In from the menu run the flow in-place (D3, chosen) or navigate to the existing `LoginPage`? In-place matches the "stay on the menu" requirement; confirm with the team.
- What are the concrete **Start Game**, **Playfields**, and **Settings** routes — new stub pages, or existing pages to reuse once built? (Assumed: stub routes for this change.)
- Should Log Out also clear the Auth0 SSO cookie (`/v2/logout`) so the next Log In re-prompts credentials, or is a silent re-login acceptable? (Assumed: local clear only.)
- Exact panning motion — single-axis drift, ping-pong, or slow diagonal orbit? (Assumed: slow bounded ping-pong; final feel tuned during implementation.)
- Does the "The Prey" title coexist with the slogan (D9, kept top-left) or should the slogan be the sole hero and the title dropped? (Assumed: keep the title top-left, balancing the top-right telemetry.)
- GPS readout precision — whole degrees (assumed, decorative and privacy-preserving) or degrees-with-decimals/DMS for a more "coordinate" feel?

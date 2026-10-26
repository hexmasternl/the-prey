# Proposal

## Why

The first thing a player sees when opening the new native Android app is the default system splash: a placeholder crosshair on a black window, followed by a bare "Standing by" welcome text. That is the app's first impression and it says nothing about the game. The Prey's identity — a night-vision, phosphor-green field-operations HUD where hunters track preys — should hit the player from the very first frame, and the app is new enough that we can set this up properly before more screens are built on top of the launch flow.

## What Changes

- Replace the placeholder crosshair launcher foreground with the real Prey mark (green scope ring + red prey head, from `designs/logo/the-prey-mark.svg`) as a vector drawable, used by both the launcher icon and the system splash.
- Give the Android system splash (core-splashscreen) an animated icon: the scope ring draws itself and the centre signal dot lights up, on the void background.
- Add an in-app, Compose-rendered **launch sequence** that continues seamlessly from the system splash (same background, same mark position) and plays a short tactical "boot" animation:
  - radar sweep and expanding ping rings behind the mark, on a faint field grid with corner-bracket chrome;
  - the red prey head inside the scope "acquired" with a brief flash;
  - "THE PREY" typed out in Special Elite, followed by the tagline "STAY HIDDEN. HUNT SMART." with the second words in signal green;
  - an UPPERCASE PT Mono boot-status line ticking through short status messages, plus a coordinate/version readout.
- The sequence runs for roughly 2.5 seconds, can be skipped with a tap, plays only on a cold start (not on configuration changes or when returning from the background), and respects the system "remove animations" setting by showing the final frame briefly instead.
- After the sequence the app navigates to the existing welcome/home screen with a quick crossfade.
- All new texts in `strings.xml` with Dutch translations.

## Capabilities

### New Capabilities
- `app-splash-screen`: what the player sees between tapping the launcher icon and the first interactive screen — the branded system splash, the in-app launch sequence, its duration, skip and reduced-motion behavior, and the hand-off to the home screen.

### Modified Capabilities
<!-- None. app-version-gate gates the home screen's buttons; the splash runs before it and does not change that behavior. -->

## Impact

- **Android app only** (`app/src`): `MainActivity.kt`, `res/values/themes.xml`, `res/drawable/` (new mark vector + animated vector), `res/mipmap-anydpi/ic_launcher.xml`, new `ui/splash/` package, `strings.xml` (en + nl).
- No new dependencies: uses the existing `core-splashscreen` and Compose (animation APIs come with the Compose BOM).
- No backend, API or infrastructure changes.
- Launcher icon changes visually from the placeholder crosshair to the real mark.

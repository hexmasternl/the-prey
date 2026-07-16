## Why

The MAUI app still ships the stock .NET template identity: the app icon and first-frame OS splash are the purple ".NET bot"/monogram on `#512BD4`, and the in-app launch screen (`WelcomePage`) shows the logo on a flat dark background. None of this matches The Prey's phosphor-green tactical brand. A player launching the app should immediately see an on-brand, "operations-grid" splash — the wolf-in-crosshair mark over a tactical elevation map — from the very first frame through the boot sequence.

## What Changes

- **Change the app identity assets** — replace the template app icon (`MauiIcon`) and native OS splash (`MauiSplashScreen`) with the brand wolf-in-crosshair mark on The Prey void-black (`#0c0e0c`) instead of the .NET monogram on purple. This covers the icon shown on the device home screen and the first-frame splash the OS renders before MAUI loads.
- **Add a "fancy" in-app splash** to the app's launch screen: a black base, a **tactical elevation/operations-grid map** as a full-bleed dimmed background, and a **larger version of the brand logo** in the foreground with signal-glow, retaining the existing corner-bracket chrome, boot-status readout, and activity indicator. This is layered onto `WelcomePage`, which is already the app's boot/launch page — so the fancy splash and the existing bootstrap/routing sequence are one screen, not two.
- **Add the brand background art** (`the-prey-background.png`) and the larger brand mark as app image resources, and register them via `MauiImage`.
- **Add the reusable tactical-splash styling** (full-bleed map background + logo/glow treatment) to the central `Colors.xaml`/`Styles.xaml` so the launch screen carries **no inline visual properties**, per the app's single-source-of-truth styling rule.

Non-goals: no change to the boot/routing logic, authentication, or navigation destinations (all owned by the existing `maui-app-front-page` change); no animation/motion beyond the existing activity indicator; no new pages.

## Capabilities

### New Capabilities
- `maui-app-branding`: The MAUI app's launch identity — the on-brand app icon and native OS splash, and the in-app "fancy" tactical splash (elevation-map background + foreground logo) presented on the launch screen while the app boots.

### Modified Capabilities
<!-- None. The launch screen's bootstrap/routing behavior is defined by the maui-app-front-page change and is unchanged here; this change only adds the splash's visual/branding requirements. -->

## Impact

- **App assets** in `src/HexMaster.ThePrey.Maui.App/Resources`: replaced `AppIcon` (icon + foreground) and `Splash/splash.svg` with the brand wolf-in-crosshair mark; new `Resources/Images/the-prey-background.png` (tactical map) and larger brand logo, sourced from `designs/Additional/` (`logo.svg`, `app-logo.png`, `the-prey-background.png`).
- **Project file** `HexMaster.ThePrey.Maui.App.csproj`: updated `MauiIcon`/`MauiSplashScreen` include + `Color` (to `#0c0e0c`); `MauiImage` entries for the new background art.
- **Theme resources** `Resources/Styles/Colors.xaml` + `Styles.xaml`: new reusable styles for the full-bleed map background and enlarged logo/glow (no inline styling on the page).
- **Launch screen** `Pages/WelcomePage.xaml`: gains the map-background layer and enlarged logo; existing hero chrome, status readout, and bindings preserved.
- **Dependencies**: none added. Uses built-in `MauiImage`/`MauiSplashScreen`/`MauiIcon` tooling only.
- **Backend**: none.

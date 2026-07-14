## Context

The MAUI app (`src/HexMaster.ThePrey.Maui.App`) carries stock template branding:

- `HexMaster.ThePrey.Maui.App.csproj` declares `MauiIcon` (`appicon.svg` + `appiconfg.svg`, `Color="#512BD4"`) and `MauiSplashScreen` (`Splash/splash.svg`, `Color="#512BD4"`) — the purple .NET monogram.
- `Pages/WelcomePage.xaml` is already the app's launch/boot screen: it shows `theprey_logo.png` (the wolf-in-crosshair mark) at `HeightRequest="132"` over a flat dark page, framed by corner brackets, with a status readout and `ActivityIndicator` driven by `WelcomeViewModel`'s bootstrap sequence.
- Brand assets live in `designs/Additional/`: `logo.svg` (full vector wolf-in-crosshair, 300×300), `app-logo.png` (clean 512² raster of the same mark), and `the-prey-background.png` (1920×1080 tactical "operations grid" — a glowing playfield polygon over near-black).
- The tactical palette already exists in `Resources/Styles/Colors.xaml` (`TpBgVoid #0c0e0c`, `TpSignal #64ff00`, etc.). The `maui-styling-expert` skill enforces one central `Colors.xaml` + `Styles.xaml` with no inline visual properties on pages.

Two distinct surfaces are in play: the **OS-owned identity** (app icon + first-frame native splash, both generated at build time from a single image + solid color) and the **in-app splash** (real XAML we control). They must not visually conflict at the hand-off frame.

## Goals / Non-Goals

**Goals:**
- Replace template app icon and native splash with the brand mark on void-black.
- Give the launch screen a layered "fancy" splash: black base → dimmed tactical elevation map → enlarged glowing logo, over the existing chrome/status.
- Keep all visual treatment in the central `Colors.xaml`/`Styles.xaml`; the page stays declarative and style-driven.
- No color flash between the native splash and the in-app splash (shared base color).

**Non-Goals:**
- No change to bootstrap/routing, auth, or navigation (owned by `maui-app-front-page`).
- No animation/motion beyond the existing `ActivityIndicator`.
- No new page or navigation stop — the splash *is* `WelcomePage`.

## Decisions

### Decision: Enhance `WelcomePage`, do not add a separate splash page

`WelcomePage` is already the first screen and already carries the logo + status while the app boots. Adding a dedicated pre-splash page would duplicate the boot screen and introduce an extra navigation hand-off (and a second color-flash risk). Instead, the "fancy splash" is realized by layering the map background and enlarging the logo on `WelcomePage`.

- **Alternative considered — dedicated `SplashPage` shown before `WelcomePage`:** rejected; adds a page, timing/teardown logic, and a redundant boot surface for no visual gain.
- **Alternative considered — richer native `MauiSplashScreen` composition:** not possible. `MauiSplashScreen` supports only a single centered vector on one solid `Color`; it cannot composite a background image plus a foreground logo. So the map-plus-logo composition must be in-app.

### Decision: Layer with a `Grid` overlay, not a page `Background`

Wrap the existing content in a `Grid` where the same cell stacks, back-to-front: (1) the page's void-black base, (2) an `Image` of the elevation map with `Aspect="AspectFill"` covering all rows/columns and dimmed via style, (3) the existing hero + status content. `AspectFill` guarantees full-bleed coverage across aspect ratios (per the "no letterbox" scenario); a `Background`/`ImageBrush` on the page is harder to dim consistently and to keep behind the corner-bracket chrome.

### Decision: Source assets — SVG for OS identity, raster map for the background

- **App icon:** `MauiIcon Include="Resources\AppIcon\appicon.svg" ForegroundFile=<brand mark svg>` with `Color="#0c0e0c"`. The vector brand mark (from `designs/Additional/logo.svg`) is copied into `Resources/AppIcon/` as the foreground so the resizetizer renders every density/adaptive shape crisply, mark kept within the adaptive safe area. `appicon.svg` (the background rect) is recolored to `#0c0e0c`.
- **Native splash:** `MauiSplashScreen Include=<brand mark svg>` with `Color="#0c0e0c"` and an appropriate `BaseSize`, replacing `splash.svg`'s .NET monogram.
- **In-app map background:** `the-prey-background.png` copied to `Resources/Images/` and registered via `MauiImage`. Raster is correct here — it's photographic-grade tactical art, not a glyph.
- **In-app foreground logo:** reuse the existing `theprey_logo.png` (already the wolf mark) or the cleaner `app-logo.png`, enlarged via a style (`~200–240` height vs the current 132). Keeping the existing image key avoids a rename ripple; swapping to `app-logo.png` is a drop-in if higher fidelity is wanted.

### Decision: All treatment via central styles

New keys added to `Colors.xaml`/`Styles.xaml`:
- A `SplashMapBackground` style for the map `Image` (`Aspect=AspectFill`, dimming `Opacity`, `InputTransparent`).
- A `SplashLogo` style (enlarged `HeightRequest`, `AspectFit`, existing `SignalGlow` shadow) — or extend the existing logo treatment.
- Reuse existing `TpBgVoid`, `SignalGlow`, `CornerBracket`, `TacticalTitle`, `HudLabel`, `StatusLabel`.

The page sets only `Style="{StaticResource ...}"` and `Source=`/`Text=` — no color/opacity/size literals — satisfying the "no inline visual properties" requirement and the styling skill's single-source-of-truth rule.

### Decision: Shared base color eliminates the hand-off flash

Native splash `Color`, the page base, and the map's darkest pixels are all `#0c0e0c` (`TpBgVoid`). When the OS splash tears down and `WelcomePage` appears, the background is identical, so there is no perceptible flash.

## Risks / Trade-offs

- **Map legibility vs. content** → The full-bleed map could compete with the logo/status text. Mitigation: dim the map via a style `Opacity` (and/or a subtle scrim) tuned so `TpText`/`TpSignal` foreground stays high-contrast; the map art is already dark near center where the logo sits.
- **Large background raster inflates app size / memory** → `the-prey-background.png` is a full-HD image bundled per-density by MAUI. Mitigation: register with a sensible `MauiImage` `BaseSize`/resize so it is not upscaled beyond need; it is a single launch asset, loaded once.
- **Adaptive-icon clipping** → the crosshair mark extends near the artboard edge and could be clipped by round masks. Mitigation: ensure the foreground SVG keeps the mark within the ~66% adaptive safe area; verify on Android.
- **Aspect-ratio cropping of the map** → `AspectFill` crops edges on tall/wide screens. Acceptable: the informative content (polygon, crosshair) is centered; edge crop only trims empty grid.
- **Build-time asset generation is platform-tooling dependent** → icon/splash regeneration happens in the resizetizer at build. Mitigation: verify with an Android build (`dotnet build -f net10.0-android`) and a visual check of the generated icon/splash.

## Migration Plan

Additive/asset-swap only; no runtime or data migration. Steps: add brand assets to `Resources/`, update `.csproj` icon/splash/image entries and colors, add the styles, layer `WelcomePage`, then rebuild so the resizetizer regenerates icon/splash. Rollback = revert the asset files, `.csproj`, styles, and `WelcomePage.xaml` (all under source control); no external state changes.

## Open Questions

- Foreground logo asset: keep `theprey_logo.png` or switch to the cleaner `app-logo.png`? (Default: keep the existing key; swap only if fidelity at the larger size warrants it.)
- Add a faint scrim/vignette over the map for extra legibility, or rely on `Opacity` dimming alone? (Default: dimming alone; add scrim only if contrast testing requires it.)

## 1. Brand assets into the app

- [x] 1.1 Copy the tactical map art `designs/Additional/the-prey-background.png` into `src/HexMaster.ThePrey.Maui.App/Resources/Images/` (renamed to `theprey_background.png` to match the `theprey_logo.png` convention and avoid hyphen-sanitization ambiguity)
- [x] 1.2 Copy the vector brand mark `designs/Additional/logo.svg` into `Resources/AppIcon/` as the app-icon foreground (flattened to a single-root SVG for the resizetizer; viewBox padded so the mark sits within the ~66% adaptive safe area), and the same mark into `Resources/Splash/splash.svg`
- [x] 1.3 Confirm the foreground launch logo asset — kept existing `Resources/Images/theprey_logo.png` (already the wolf-in-crosshair mark)

## 2. App icon & native OS splash (identity)

- [x] 2.1 Recolor `Resources/AppIcon/appicon.svg` background rect from `#512BD4` to `#0c0e0c`
- [x] 2.2 Update `MauiIcon` in `HexMaster.ThePrey.Maui.App.csproj` to use the brand-mark foreground and `Color="#0c0e0c"`
- [x] 2.3 Replace `Resources/Splash/splash.svg` content with the brand wolf-in-crosshair mark and update `MauiSplashScreen` `Color` to `#0c0e0c` (dropped `#512BD4`), `BaseSize="180,180"`
- [x] 2.4 Register the new map art via `MauiImage` in the `.csproj` (`Resize="False"`, `BaseSize="1920,1080"` so the full-HD raster is not upscaled)

## 3. Central tactical-splash styles

- [x] 3.1 In `Resources/Styles/Styles.xaml`, added a `SplashMapBackground` style for the map `Image` (`Aspect=AspectFill`, `Opacity=0.4`, `InputTransparent=True`); plus a `SplashBase` BoxView style (`TpBgVoid`) so the in-app base matches the native splash color
- [x] 3.2 In `Resources/Styles/Styles.xaml`, added a `SplashLogo` style (`HeightRequest=220`, `AspectFit`, `Shadow={StaticResource SignalGlow}`)
- [x] 3.3 No new color/brush keys needed — reused existing `TpBgVoid`, `TpSignal`, `SignalGlow`, `CornerBracket`, `TacticalTitle`, `HudLabel`, `StatusLabel`

## 4. Layer the fancy splash onto WelcomePage

- [x] 4.1 Wrapped `Pages/WelcomePage.xaml` content in a stacking `Grid`: `SplashBase` void-black base, then the map `Image` (`Style=SplashMapBackground`), then the existing hero + status content on top
- [x] 4.2 Enlarged the foreground logo via `Style="{StaticResource SplashLogo}"` (removed the inline `HeightRequest="132"`/`Aspect`/`Shadow` literals from the page)
- [x] 4.3 Verified the page sets only `Style`, `Source`, `Text`, and layout structure — no inline color/opacity/size/glow literals; corner brackets, wordmark, tagline, status label, and `ActivityIndicator` bindings preserved

## 5. Verification

- [ ] 5.1 Build for Android (`dotnet build ... -f net10.0-android`) so the resizetizer regenerates icon + native splash — **blocked**: the app is currently deployed/running on the Android emulator, which holds `obj/.../android/assets/x86_64/HexMaster.ThePrey.Maui.App.dll` (error `XARDF7024`). Verified compilation instead via the **Windows head** (`-f net10.0-windows10.0.19041.0`, build succeeded, 0 errors) which runs the same XAML SourceGen, SVG resizetizer, and font registration. Stop the running app on the emulator, then re-run the Android build.
- [ ] 5.2 Visually confirm on device/emulator: home-screen icon shows the brand mark on void-black (no purple/monogram); adaptive-icon mask does not clip the mark — *manual on-device check*
- [ ] 5.3 Visually confirm the launch screen: black base + full-bleed dimmed elevation map + enlarged glowing logo, with legible wordmark/tagline/status/activity indicator, and no color flash on hand-off from the native splash — *manual on-device check*
- [ ] 5.4 Confirm full-bleed map coverage with no letterbox bars across a phone and a tablet/large-screen form factor — *manual on-device check*

## 6. Tactical fonts (added on request)

- [x] 6.1 Copy `designs/SpecialElite-Regular.ttf` and `designs/PTMono-Regular.ttf` into `Resources/Fonts/` (picked up by the existing `MauiFont` glob)
- [x] 6.2 Register both faces in `MauiProgram.CreateMauiApp` (`SpecialElite`, `PTMono`)
- [x] 6.3 Point the central font tokens at them in `Styles.xaml`: `TpDisplayFont`→`SpecialElite` (headings/wordmark), `TpBodyFont`→`PTMono` (HUD labels, status readouts, body) — replacing the monospace `OnPlatform` fallbacks

---
name: project-game-location
description: Native Android foreground service for background GPS tracking in the prey game role
metadata:
  type: project
---

The prey role requires continuous GPS broadcasting to POST /games/{gameId}/location even when the screen is locked. This is implemented via a native Android foreground service.

**Architecture decision:** Two-tier design — native service for Android, Capacitor Geolocation + HttpClient fallback for web/browser.

**Why:** Android kills background JS after ~60 s of screen lock. A foreground service with a persistent notification is the only way to keep GPS + network running reliably on Android.

**How to apply:** When touching location, auth, or game lifecycle code, remember the location service is stateful — it runs independently of the Angular lifecycle. Always call stopTracking() in ngOnDestroy AND on game-ended SSE event.

Key files added:
- `android/.../LocationForegroundService.kt` — Kotlin foreground service using FusedLocationProviderClient + OkHttp
- `android/.../GameLocationPlugin.kt` — Capacitor plugin bridge (name: "GameLocation")
- `android/.../MainActivity.java` — registerPlugin(GameLocationPlugin.class) added to onCreate (before super)
- `src/app/games/game-location-plugin.ts` — TypeScript plugin definition + registerPlugin()
- `src/app/games/game-location-web-stub.ts` — WebPlugin no-op stub for browser builds
- `src/app/games/game-location.service.ts` — Angular service wrapping both paths
- `src/app/games/game-prey.page.ts` — updated to use GameLocationService; separate Capacitor watch for map marker

**Interval sync:** pollStatus() calls locationService.updateInterval(nextPingDuration * 1000) after each server response. On native this sends ACTION_UPDATE_INTERVAL to the running service without restarting it.

**Kotlin enablement:** android/build.gradle adds kotlin-gradle-plugin:2.1.0 classpath; android/app/build.gradle applies kotlin-android plugin. MainActivity remains Java; it can reference Kotlin classes fine.

**Dependencies added to app/build.gradle:**
- com.google.android.gms:play-services-location:21.3.0
- com.squareup.okhttp3:okhttp:4.12.0 (pin; already transitive via Capacitor)

[[project-game-lobby]]

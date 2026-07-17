## 1. Localized strings

- [x] 1.1 Add `Debug_JoinPromptTitle` and `Debug_JoinPromptMessage` (and a placeholder key if used) to the default `AppResources.resx`.
- [x] 1.2 Add the Dutch translations of those keys to the `.nl` resx.
- [x] 1.3 Confirm the strings are reachable through the localization service (same access pattern as existing menu strings).

## 2. Debug tap affordance on the Home page

- [x] 2.1 Constructor-inject `IInviteDeepLinkHandler` into `HomePage` (verify it is registered in DI / `MauiProgram`).
- [x] 2.2 In `HomePage` code-behind, inside `#if DEBUG`, attach a `TapGestureRecognizer` to `PlayerNameLabel` (wire it up after `InitializeComponent`, e.g. in the constructor or `OnAppearing`).
- [x] 2.3 Ensure the gesture wiring and its handler are fully compiled out in release builds (all new code paths under `#if DEBUG`).

## 3. Join-URL prompt + routing

- [x] 3.1 Implement the tap handler: call `DisplayPromptAsync` with the localized title/message to collect a Join URL.
- [x] 3.2 Short-circuit on cancel (null) and empty/whitespace input — no navigation, no error.
- [x] 3.3 Parse the entered text into an absolute `Uri`; on failure, quietly no-op.
- [x] 3.4 Pass the `Uri` to `IInviteDeepLinkHandler.TryHandleAsync(uri)`; ignore a `false` result (invalid URL → no navigation).
- [x] 3.5 Confirm a valid URL routes to `JoinGamePage` with the `gameId` query and the join flow proceeds as from a deep link. (Static: reuses the same `IInviteDeepLinkHandler` seam the invite deep link uses, so routing is identical by construction.)

## 4. Verify

- [x] 4.1 Build the MAUI app in DEBUG and RELEASE; confirm it compiles in both and the affordance is absent in RELEASE. (Both builds: 0 warnings, 0 errors; affordance guarded by `#if DEBUG`.)
- [ ] 4.2 Debug run: sign in, tap the player name, paste `https://theprey.nl/join/{gameId}` → lands on the Join Game page for that id. *(Manual — needs an interactive device/emulator run with a live game; not runnable in this environment.)*
- [ ] 4.3 Debug run: submit a malformed URL and an empty value → no navigation, no crash. *(Manual — needs an interactive device/emulator run.)*
- [x] 4.4 Run existing MAUI unit tests to confirm no regressions in the invite deep-link / join view models. (513 passed, 0 failed.)

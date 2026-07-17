## Why

Joining an existing game in the MAUI app currently requires opening an invite deep link (`https://theprey.nl/join/{gameId}`) from outside the app — which is awkward on a development machine or emulator where deep links do not always resolve. During development and testing we need a friction-free way to jump straight into the join flow by pasting an invite URL we already have, without touching production entry points.

## What Changes

- In **debug builds only**, the signed-in player's display name on the Home page becomes tappable.
- Tapping it opens a prompt dialog asking for a Join URL (e.g. `https://theprey.nl/join/e3b0922a-c6cc-4d6a-b889-0e7a0868433f`).
- On confirming a valid URL, the app parses the game id out of the URL and navigates to the existing Join Game page for that game id — exactly as if the invite deep link had been followed.
- Invalid or empty input cancels quietly (no navigation, no crash); the debug affordance is entirely absent from release builds.
- Reuses the existing invite-URL parsing and join navigation seam (`IInviteDeepLinkHandler`) rather than duplicating URL/route logic.

## Capabilities

### New Capabilities
- `maui-debug-join-entry`: A debug-only Home-page affordance that lets a developer paste an invite Join URL and be routed into the existing Join Game flow for that game id.

### Modified Capabilities
<!-- No requirement-level changes to existing capabilities; the join page and deep-link parsing are reused as-is. -->

## Impact

- **Affected code (MAUI app, `src/Maui/HexMaster.ThePrey.Maui.App`)**:
  - `Pages/HomePage.xaml` / `Pages/HomePage.xaml.cs` — add a debug-only tap gesture on `PlayerNameLabel` and the prompt-dialog flow.
  - Reuses `Services/Navigation/InviteDeepLinkHandler` (`IInviteDeepLinkHandler.TryHandleAsync(Uri)`) for parsing + routing, and the existing `JoinGamePage` / `JoinGameViewModel` join flow.
  - New localized strings (English + Dutch) for the prompt title/message via `AppResources.resx`.
- **No backend, API, or release-build behavior changes.** The affordance is compiled out of release builds (`#if DEBUG`).
- **No new dependencies.**

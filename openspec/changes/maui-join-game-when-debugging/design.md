## Context

The MAUI app already has a complete join flow:

- `InviteDeepLinkHandler` (`IInviteDeepLinkHandler`) parses an HTTPS invite URL of the form `https://{host}/{segment}/{gameId}` (host/segment derived from `ThePreyClientOptions.JoinLinkBaseUrl`, id must be a `Guid`) and navigates to the `join` Shell route with the `gameId` query.
- `JoinGamePage` + `JoinGameViewModel` render the join page, gate on sign-in, take a 4-digit code, and POST the join.

The only gap this change fills is a **debug-time trigger**: on a dev machine or emulator the OS deep link often does not resolve, so there is no easy way to exercise the join flow. We want to paste a Join URL we already have and land on the join page.

The Home page shows the signed-in player's display name as a `PlayerNameLabel` (built as per-character `Span`s in code-behind for the running-light animation, bound-visible via `MainMenuViewModel.ShowPlayerName`).

## Goals / Non-Goals

**Goals:**
- A debug-only affordance to enter the join flow by pasting an invite URL.
- Zero footprint in release builds (compiled out with `#if DEBUG`).
- Reuse existing URL parsing + routing (`IInviteDeepLinkHandler`) so behavior cannot drift from the real invite deep link.
- Localized prompt text (English + Dutch), per the app's single-source-of-truth localization rule.

**Non-Goals:**
- No changes to the join page, join view model, or backend.
- No release-build UI surface or menu button for joining by URL.
- No new abstraction/service — this is a thin, debug-only page concern.

## Decisions

### Decision 1: Trigger via a tap gesture on the existing `PlayerNameLabel`, in code-behind, `#if DEBUG`-guarded

The request is specifically to click the display name. `PlayerNameLabel` already exists and is only visible when signed in with a resolved name — a natural, unobtrusive hook. Wiring a `TapGestureRecognizer` in code-behind inside `#if DEBUG` keeps the affordance out of release builds entirely and avoids polluting the XAML or the view model with debug-only concerns.

- **Alternative considered — bind a command on `MainMenuViewModel`:** rejected. It would add debug-only surface to a production view model and still needs the MAUI `DisplayPromptAsync` (a page concern), so nothing is gained.
- **Alternative considered — a debug menu button in XAML:** rejected. Harder to compile out cleanly (XAML has no `#if DEBUG`), and the user explicitly asked for a tap on the name.

### Decision 2: Collect the URL with `Page.DisplayPromptAsync`

`DisplayPromptAsync(title, message, ..., placeholder)` is the built-in single-line text prompt with OK/Cancel and returns `null` on cancel — exactly the "enter a URL, click OK" interaction requested. No custom dialog page needed.

### Decision 3: Parse + route by reusing `IInviteDeepLinkHandler.TryHandleAsync(Uri)`

Rather than re-parse the URL and rebuild the `join?gameId=...` route in the page, construct a `Uri` from the entered text and hand it to the already-registered `IInviteDeepLinkHandler`. It applies the same host/path/GUID validation the real deep link uses and performs the navigation, returning `false` on rejection. This guarantees the debug entry and the invite deep link stay in lockstep.

- The page resolves `IInviteDeepLinkHandler` from the DI container (via `Handler` / `Application.Current.Handler.MauiContext.Services` or constructor injection).
- Empty/whitespace input and `Uri` construction failures short-circuit before calling the handler; a rejected handler result is ignored (quiet no-op).

### Decision 4: Constructor-inject the handler into `HomePage`

`HomePage` is already DI-resolved (its `MainMenuViewModel` is injected). Add an `IInviteDeepLinkHandler` constructor parameter (used only inside `#if DEBUG` code paths) so the debug flow has no service-locator lookups. The parameter itself is harmless in release; only the tap wiring and prompt handler are `#if DEBUG`.

### Decision 5: Localized prompt strings

Add `Debug_JoinPromptTitle` and `Debug_JoinPromptMessage` (plus placeholder if needed) to `AppResources.resx` and the Dutch `.resx`, consumed via the localization service — honoring the app's no-hard-coded-strings rule even for debug UI.

## Risks / Trade-offs

- **[Constructor param present in release builds even though unused]** → It is a cheap DI resolution with no behavioral effect; the actual affordance (gesture + prompt) is fully `#if DEBUG`-guarded, so release UX is unchanged. Acceptable.
- **[Debug affordance discoverability is intentionally low]** → Tapping a name is not self-evident; that is desired (it is a developer tool, not a feature). Documented in the tasks so testers know it exists.
- **[User pastes a code URL / wrong-host URL]** → The reused handler rejects anything not matching the configured host/path/GUID, so bad input is a quiet no-op rather than a crash or a bogus navigation.
- **[Prompt shown before the name resolves]** → The label is hidden until the name resolves and the gesture lives on that label, so there is no target to tap in that window.

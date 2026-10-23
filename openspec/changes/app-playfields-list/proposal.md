# Proposal

## Why

The main menu's **Playfields** button still opens a placeholder. Players need a place to see the playfields they have drawn, clean up ones they no longer use, find public playfields other players have shared, and start creating a new one. The backend already offers everything this needs (list own, search public, delete), so this is a client-only screen.

## What Changes

- Replace the Playfields placeholder with a **playfields list screen** that has two tabs, **Private** and **Public**. The Private tab is selected every time the screen is opened.
- **Private tab**: downloads the playfields the signed-in player owns (`GET /playfields`) and shows each as a list item with its details: name, visibility (private/public), last-updated date and centre coordinates. Loading, empty and error (with retry) states are shown. The list refreshes when the player returns to the screen.
- **Swipe to delete** on the Private tab: sliding an item to the left reveals a **Delete** button. Tapping it opens a confirmation dialog; only after confirming is `DELETE /playfields/{id}` sent. The item disappears on success; on failure it stays and the player sees an error message. A screen-reader action offers the same delete without swiping. Owners can delete their own playfields whether they are private or public.
- **Create button**: a floating action button in the bottom-right corner of the Private tab opens a new **playfield editor** destination, a placeholder in this change (the editor is a separate change).
- **Public tab**: shows a search field that receives focus (and opens the keyboard) when the tab is selected. Typing sends `GET /playfields/public?q=…` once the trimmed text has **3 or more characters**, debounced by **366 ms**; a newer query supersedes an older in-flight one. Fewer than 3 characters clears the results and shows a hint. The server returns only public playfields.
- **Public playfields can never be removed from the Public tab**: its items have no swipe, no delete button and no delete action.
- All new texts in English and Dutch.
- `PlayFieldSummaryDto` model and a Retrofit `PlayFieldApi` in the app; no new libraries.

## Capabilities

### New Capabilities
- `app-playfields-list`: the Android playfields list screen — its Private and Public tabs, loading/empty/error states, list item details, swipe-to-delete with confirmation, the create button and where it leads, and the debounced public search.

### Modified Capabilities
- `playfields`: "List visible play fields" currently says the list includes other players' public playfields; the implemented endpoint (and the Private tab that depends on it) returns only the caller's own playfields, with public discovery going through the public search endpoint. The requirement is corrected to match.

## Impact

- **Android app** (`app/src`): new `data/playfield/` (API + DTO + repository), new `ui/playfields/` (screen, view model, swipe row, delete dialog), `ui/navigation/ThePreyNavHost.kt` (real Playfields destination, new playfield-editor placeholder route), `AppContainer.kt`, `strings.xml` (en + nl), unit tests.
- **Backend:** no code change. Uses existing `GET /playfields`, `GET /playfields/public?q=` and `DELETE /playfields/{id}`.
- **Dependencies:** none new (Compose foundation's anchored dragging, Material 3 tabs/dialogs, Retrofit already present).

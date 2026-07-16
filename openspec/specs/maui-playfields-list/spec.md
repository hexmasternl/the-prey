# maui-playfields-list Specification

## Purpose
Define the MAUI playfields list page with Private and Public tabs: a local-first, cached view of the user's own playfields and a debounced search over public playfields, with loading, empty, and error states, all styled through the central resource dictionaries.

## Requirements
### Requirement: Playfields list page with Private/Public tabs

The MAUI app SHALL present a playfields list page (replacing the `PlayfieldsPage` stub) with a tab control offering two tabs, **Private** and **Public**. The **Private** tab SHALL be the active tab when the page is first shown. Selecting a tab SHALL switch the visible content to that tab's list without leaving the page. All visual treatment (tab styling, list items, badges) SHALL be applied through the central `Colors.xaml` / `Styles.xaml` resources; the page SHALL NOT declare colors, sizes, opacity, borders, or glow as inline/local properties.

#### Scenario: Private tab active by default

- **WHEN** the playfields list page is first displayed
- **THEN** the Private tab is the active tab
- **AND** the private playfields content is shown

#### Scenario: Switching to the Public tab

- **WHEN** the user selects the Public tab
- **THEN** the Public tab becomes active
- **AND** the public search field and its results are shown

#### Scenario: No inline visual properties

- **WHEN** the playfields list page XAML is reviewed
- **THEN** its visual treatment (tab styling, list items, badges, colors, sizing) is applied through named styles and `StaticResource` color keys
- **AND** no color, opacity, border, glow, or size literal is declared inline on the page's visual elements

### Requirement: Private tab lists the user's own playfields

When the Private tab is shown (on the page appearing), the app SHALL present the current user's playfields, listing each one with its **name** and a **badge** indicating whether the playfield is `PUBLIC` or `PRIVATE`, derived from the playfield's visibility flag. The app SHALL refresh the list from the backend (see the local-first caching requirement) so the displayed list reflects the server once a refresh succeeds.

#### Scenario: Private playfields render with name and badge

- **WHEN** the playfields list page appears with the Private tab active and playfields are available (from cache and/or a successful refresh)
- **THEN** each playfield is listed showing its name and a `PUBLIC` or `PRIVATE` badge matching its visibility

#### Scenario: User has no playfields

- **WHEN** the private list has no cached entries and the backend refresh succeeds and returns no playfields
- **THEN** the Private tab shows an empty-state message rather than an error or a blank list

#### Scenario: Private refresh fails with nothing cached

- **WHEN** the private playfields refresh fails (network error or the session cannot be established) and there is no cached list to show
- **THEN** the Private tab shows an error state
- **AND** the page remains usable and the user can switch tabs

### Requirement: Private list is cached for instant display

The app SHALL cache the current user's downloaded private playfields in on-device storage and use that cache to make the Private tab **local-first**. When the Private tab appears, the app SHALL display the cached list **immediately** (without waiting for a network request) and then refresh from the backend in the background. On a successful refresh, the app SHALL replace the displayed list with the server's result and SHALL overwrite the cache with it. When the background refresh fails, the app SHALL keep the cached list displayed rather than replacing it with an error. The cache SHALL cover the private list only; public search results SHALL NOT be cached.

#### Scenario: Cached private list shown immediately

- **WHEN** the Private tab appears and a cached private list exists from a previous visit
- **THEN** the app displays the cached playfields immediately, before any backend response
- **AND** it starts a background refresh from the backend

#### Scenario: Successful refresh updates the list and the cache

- **WHEN** the background refresh of the private list succeeds
- **THEN** the displayed list is replaced with the playfields returned by the backend
- **AND** the cache is overwritten with that same list so the next visit renders it immediately

#### Scenario: Failed refresh keeps the cached list

- **WHEN** a cached private list is displayed and the background refresh fails (network error or the session cannot be established)
- **THEN** the cached list remains displayed
- **AND** the Private tab does not show an error state in place of the cached list

#### Scenario: First run with no cache

- **WHEN** the Private tab appears and no cached private list exists yet
- **THEN** the app shows the loading indication while it fetches the list from the backend
- **AND** on success it displays and caches the returned playfields (or shows the empty state when none are returned)

### Requirement: Public tab searches public playfields

The Public tab SHALL show a search input. When the trimmed search text contains **at least three characters**, the app SHALL request matching public playfields from the backend and list each result showing its **name** and a `PUBLIC` badge. When the trimmed search text contains **fewer than three characters**, the app SHALL NOT send a request and SHALL show no results (an idle/prompt state).

#### Scenario: Query reaches three characters

- **WHEN** the user's trimmed search text reaches at least three characters
- **THEN** the app requests public playfields matching the query from the backend
- **AND** each matching playfield is listed with its name and badge

#### Scenario: Query below three characters sends nothing

- **WHEN** the trimmed search text contains fewer than three characters
- **THEN** no search request is sent
- **AND** the results list is empty / shows the idle prompt

#### Scenario: Search returns no matches

- **WHEN** a search request succeeds and returns no matching public playfields
- **THEN** the Public tab shows a no-results state rather than an error

#### Scenario: Search fails

- **WHEN** a search request fails (network error or the session cannot be established)
- **THEN** the Public tab shows an error state
- **AND** the page remains usable

### Requirement: Public search is debounced

The public search SHALL be debounced by 300 milliseconds: while the user keeps typing, no request SHALL be sent. A request SHALL be sent only after the input has been idle for the debounce window. When new input arrives before an earlier debounced or in-flight search has produced results, the earlier search SHALL be cancelled/superseded so that only the results of the most recent query are shown.

#### Scenario: Rapid typing sends a single request

- **WHEN** the user types several characters in quick succession within the debounce window
- **THEN** no request is sent while typing continues
- **AND** a single request is sent for the final query once typing pauses for the debounce window

#### Scenario: Newer query supersedes an older one

- **WHEN** the user changes the query before the previous search's results arrive
- **THEN** the previous search is cancelled or its results are discarded
- **AND** only the most recent query's results are displayed

### Requirement: Loading indication during requests

The page SHALL show a busy/loading indication while a private load or a public search request is in flight, and SHALL clear it when the request completes (success, empty, or error).

#### Scenario: Busy while loading

- **WHEN** a private load or public search request is in flight
- **THEN** the page shows a loading indication
- **AND** the indication clears once the request completes

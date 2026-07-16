## ADDED Requirements

### Requirement: Settings page with display name and language

The MAUI app SHALL present a settings page (replacing the `SettingsPage` stub) reached from the main-menu **Settings** button. The page SHALL show an editable **display name** field and a **language toggle** offering **English** and **Dutch**. When the page appears it SHALL load the current user's settings from the backend and pre-fill the display name and language from the loaded values. All visual treatment SHALL be applied through the central `Colors.xaml` / `Styles.xaml` resources and all text through localized string lookups; the page SHALL NOT declare colors, sizes, opacity, borders, or glow as inline/local properties.

#### Scenario: Settings load on appearing

- **WHEN** the settings page is displayed
- **THEN** the app requests the current user's settings from the backend
- **AND** the display name field is pre-filled with the current display name
- **AND** the language toggle reflects the current preferred language

#### Scenario: Settings load fails

- **WHEN** the settings request fails (network error or the session cannot be established)
- **THEN** the page shows an error state
- **AND** the page does not crash

#### Scenario: No inline visual properties

- **WHEN** the settings page XAML is reviewed
- **THEN** its visual treatment is applied through named styles and `StaticResource` color keys
- **AND** no color, opacity, border, glow, or size literal is declared inline on the page's visual elements

### Requirement: Display name auto-saves with a 300 ms debounce

Editing the display name field SHALL automatically save the new value to the backend, debounced by 300 milliseconds. While the user keeps typing, no request SHALL be sent; a single update request SHALL be sent only after the input has been idle for the debounce window. When new input arrives before an earlier debounced or in-flight save has completed, the earlier save SHALL be cancelled/superseded so only the most recent value is persisted. A blank or whitespace-only display name SHALL NOT be sent (the backend requires a non-empty display name).

#### Scenario: Rapid typing sends a single save

- **WHEN** the user types several characters into the display name in quick succession within the debounce window
- **THEN** no save request is sent while typing continues
- **AND** a single update request is sent for the final value once typing pauses for the debounce window

#### Scenario: Newer edit supersedes an older save

- **WHEN** the user changes the display name again before the previous save completes
- **THEN** the previous save is cancelled or its result is discarded
- **AND** only the most recent value is persisted

#### Scenario: Blank display name is not sent

- **WHEN** the trimmed display name is empty
- **THEN** no save request is sent
- **AND** the page indicates the display name is required

#### Scenario: Save reflects success and failure

- **WHEN** a display name save completes successfully
- **THEN** the page indicates the change was saved
- **WHEN** a display name save fails
- **THEN** the page indicates the save failed without losing the edited text

### Requirement: Language toggle switches the app language and saves the preference

Flipping the language toggle SHALL immediately switch the app's UI language to the selected language, persist the choice locally, and save the selected language to the backend. The local switch and local persistence SHALL take effect independently of the backend save, so that a failed backend save does not revert the visible language.

#### Scenario: Toggling language switches the UI live

- **WHEN** the user flips the language toggle to the other language
- **THEN** the app's visible text switches to the selected language immediately without a restart
- **AND** the selected language is persisted locally

#### Scenario: Toggling language saves to the backend

- **WHEN** the user flips the language toggle
- **THEN** the app sends an update request setting the preferred language to the selected value

#### Scenario: Backend save fails but language still switches

- **WHEN** the language toggle is flipped and the backend save fails
- **THEN** the app's visible language still reflects the selected language
- **AND** the page indicates the save failed without reverting the toggle

### Requirement: Loading indication during requests

The page SHALL show a busy/loading indication while the initial settings load is in flight, and SHALL clear it when the load completes (success or error).

#### Scenario: Busy while loading

- **WHEN** the initial settings load is in flight
- **THEN** the page shows a loading indication
- **AND** the indication clears once the load completes

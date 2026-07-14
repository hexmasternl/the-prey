## Why

The MAUI main menu's **Settings** button routes to `SettingsPage`, a "Coming soon" stub. Players have no way to change how their name is shown or to read the app in their own language. This change turns Settings into a working screen for editing the **display name** and switching the app **language**, and — because a language switch is meaningless without translations — introduces app-wide localization (English + Dutch) with the device language as the default. The backend already stores both fields (`PreferredLanguage`, `DisplayName` on `UserDto`) and exposes the endpoints to read and update them, so this is the client catching up to an existing contract.

## What Changes

- Replace the `SettingsPage` stub with a real **settings page** in The Prey tactical aesthetic. On appearing it loads the current user's settings (`GET /users/me`) and shows an editable **display name** field and a **language toggle** (English / Dutch) reflecting the current preference.
- **Display name**: editing the field auto-saves to the backend, **debounced by 300 ms** — while the user keeps typing nothing is sent; a single `PUT /users/me` fires once typing pauses. An in-flight save superseded by newer input is cancelled so only the latest value is persisted. Empty/whitespace names are not sent (the field is required by the backend).
- **Language toggle**: flipping the toggle **immediately** (a) switches the app's UI language live, (b) persists the choice locally, and (c) saves it to the backend (`PUT /users/me`). No debounce — a toggle is a single deliberate action.
- **Make the app multi-lingual**: introduce a localization layer with **English** and **Dutch** resource strings, a XAML translate mechanism that re-renders when the language changes at runtime, and a language resolver whose default is the **device language** (mapped to `en`/`nl`; anything other than Dutch falls back to English). Existing pages' hard-coded UI strings are migrated to resource keys so the whole app honours the selected language.
- Introduce the client-side seam to call the **authenticated** users endpoints: a typed users API client that reads the current user and updates the display name + language, reusing the access-token accessor seam (`IAccessTokenProvider`) and the `GameApiClient` result-mapping pattern.
- Show clear **loading**, **saved/saving**, and **error** states; a denied/expired session or a failed save degrades gracefully rather than crashing, and the language switch still applies locally even if the server save fails.

## Capabilities

### New Capabilities
- `maui-app-localization`: App-wide internationalization — English and Dutch resource strings, a runtime-switchable localization service with a XAML `Translate` markup extension that re-renders on language change, a language resolver defaulting to the device language (mapped to `en`/`nl`), a locally persisted language preference applied at startup, and migration of existing pages' static UI strings to resource keys.
- `maui-settings-page`: The settings page itself — loading the current user's settings, the editable display name with 300 ms-debounced auto-save, the English/Dutch language toggle that switches the UI live and saves the preference, and the loading/saving/error states, all in central-styles-only tactical styling.
- `maui-users-client`: The client-side authenticated access to the users endpoints — a typed users API client that retrieves the current user (`GET /users/me`) and updates the display name + preferred language (`PUT /users/me`), mapping backend status codes to result types, reusing the access-token seam.

### Modified Capabilities
<!-- None. The existing SettingsPage is a stub with no capability spec; the main menu's Settings navigation route is unchanged. No archived capability's requirements change. The backend Users endpoints already exist and are unchanged. -->

## Impact

- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - `Pages/SettingsPage.xaml` (+ `.xaml.cs`): rebuilt from the stub into the settings form; `OnAppearing` loads the current settings. No inline visual literals (single-source-of-truth styling rule).
  - New `ViewModels/SettingsViewModel.cs`: exposes the display name (with 300 ms debounce), the selected language, and loading/saving/error state; consumes the users client, the access-token provider, the localization service, and `TimeProvider`.
  - New `Services/Api/IUserApiClient.cs` + `UserApiClient.cs`: typed `HttpClient` for `GET /users/me` and `PUT /users/me`, mapping 200/400/401/error like the existing `GameApiClient`.
  - New client model `UserSettings` (DisplayName, PreferredLanguage) mirroring the relevant `UserDto` fields.
  - New localization layer under `Resources/Strings/` (`AppResources.resx`, `AppResources.nl.resx`) and `Services/Localization/`: `ILocalizationService`/implementation (runtime culture switch + `PropertyChanged`), a `TranslateExtension` markup extension, `ILanguageResolver` (device-language default) and a persisted `ILanguageStore` (over MAUI `Preferences`).
  - Existing pages (`WelcomePage`, `LoginPage`, `HomePage`, `GamePage`, `StartGamePage`, `PlayfieldsPage`, `SettingsPage`) have their static UI strings replaced with `Translate` lookups.
  - `Resources/Styles/Styles.xaml`: new form-field, language-toggle, and status/hint styles; reuse existing `Tp*` color tokens.
  - `App.xaml.cs` / `MauiProgram.cs`: apply the resolved language at startup; register the view model, users API client (typed `HttpClient` on the backend base URL), localization service, language resolver/store, and `TimeProvider.System`.
- **Backend**: no changes. Reuses the existing `GET /users/me` (`GetCurrentUser`) and `PUT /users/me` (`UpdateCurrentUser`, which takes `DisplayName` + `PreferredLanguage`) — both `RequireAuthorization()`. Supported languages (`en`, `nl`) already match `User.SupportedLanguages`.
- **Shared seam**: reuses `IAccessTokenProvider` (the authenticated-call access-token accessor introduced by the sibling `maui-playfields-list-page` change). If that change has not yet landed, this change adds the identical seam.
- **Navigation**: the existing `settings` Shell route and the main-menu **Settings** button are unchanged; they now land on the real page.
- **Non-goals**: editing the in-game **callsign**, email, or any profile field other than the display name; languages beyond English and Dutch; per-region formatting beyond string translation; offline queueing of settings changes; changing how the backend stores or validates these fields.

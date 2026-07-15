## Context

The MAUI app (`HexMaster.ThePrey.Maui.App`) reaches `SettingsPage` from the main menu's **Settings** button (route `settings`, registered in `AppShell`). Today that page is a "Coming soon" stub, and the app is English-only with every UI string hard-coded in XAML (`Text="SETTINGS"`, etc.). This change turns Settings into a working screen and makes the whole app bilingual.

The backend already exposes what this page needs, under the authorized `/users` group:
- `GET /users/me` → `UserDto (UserId, DisplayName, Callsign, EmailAddress, PreferredLanguage)` — the caller's current settings.
- `PUT /users/me` with `UpdateUserRequest (FirstName?, LastName?, DisplayName, PreferredLanguage)` → `UserDto` — updates the display name and language together. The endpoint **requires** both `DisplayName` and `PreferredLanguage` to be non-empty (returns `ValidationProblem` otherwise) and returns `404` if the user does not exist. There is also a `PUT /users/settings` endpoint, but it updates the **callsign** (not the display name); this change targets the display name, so `PUT /users/me` is the correct endpoint.

The domain fixes the supported languages: `User.SupportedLanguages = ["en", "nl"]` and `User.DefaultLanguage = "en"`. The client mirrors exactly these two codes.

Both endpoints `RequireAuthorization()`, so calls need a bearer access token. The established client pattern is `GameApiClient`: a typed `HttpClient` (base address from `ThePreyClientOptions.BackendBaseUrl`, configured in `MauiProgram`) whose method takes the access token and maps status codes to a result union. The access token itself is acquired from the stored session; the sibling `maui-playfields-list-page` change introduces `IAccessTokenProvider` (`Task<string?> GetAccessTokenAsync(CancellationToken)`, over `ITokenStore` + `IAuth0TokenClient.RefreshAsync`, in-memory cached) as the reusable accessor. This change reuses that seam.

The tactical theme is centralized: `Colors.xaml` holds the `Tp*` palette and `Styles.xaml` holds every style. The `maui-styling-expert` rule is strict: pages carry **no** inline visual properties. View models follow a plain-.NET, MAUI-behind-interfaces pattern (`MainMenuViewModel`, `RelayCommand`, `ObservableObject`) so they are unit-testable with xUnit + Moq; MAUI/HTTP concerns sit behind interfaces. The test project is plain `net10.0` and cannot link MAUI-only types, so HTTP clients are tested via a stub `HttpMessageHandler` (`StubHttpMessageHandler`, `GameApiClientTests`).

There is no localization infrastructure today, so the runtime-switchable i18n layer is net-new.

## Goals / Non-Goals

**Goals:**
- Turn the `SettingsPage` stub into a working settings form: editable display name + English/Dutch language toggle, both auto-saved to the backend.
- Auto-save the display name with a 300 ms debounce; toggle-save the language immediately.
- Make the app bilingual (en/nl) with the **device language** as the default, a **runtime** language switch (no restart), and a persisted preference applied at startup.
- Reuse the existing session/auth building blocks, the `IAccessTokenProvider` seam, and the `GameApiClient` result-mapping pattern.
- A view model unit-testable without a device, including the debounce, the language switch, and the save mapping.

**Non-Goals:**
- Editing the in-game **callsign**, email, first/last name, or any profile field other than the display name.
- Languages beyond English and Dutch; culture-specific number/date/currency formatting (string translation only).
- Offline queueing/retry of settings changes (online-only, like the sibling MAUI screens).
- Auth0 interactive login here — the page assumes an established session (the menu only enables Settings when signed in) and degrades to an error state if the session cannot be refreshed.
- Changing the backend's storage, validation, or endpoints.

## Decisions

### D1: `SettingsViewModel` owns all page state; MAUI concerns behind interfaces

A new `SettingsViewModel : ObservableObject` exposes: `DisplayName` (two-way bound), `SelectedLanguage` (`en`/`nl`, or an `AppLanguage` enum), `IsBusy`, `IsDutch` (convenience for the toggle), and status flags (`HasLoadError`, `IsSaving`, `HasSaveError`). It depends on `IUserApiClient`, `IAccessTokenProvider`, `ILocalizationService`, and `TimeProvider` only — no MAUI/HTTP types — so it is fully mockable.
- `LoadAsync()` runs on appearing: acquire a token, call `GetCurrentUserAsync`, populate `DisplayName`/`SelectedLanguage` from the result, and align the app language to the loaded preference; map no-token/Unauthorized/Error to the load-error state.
- The `DisplayName` setter drives the debounced save (D3); the language toggle drives the immediate save + live switch (D4).
- *Guard:* while `LoadAsync` populates `DisplayName`/`SelectedLanguage` from the server, suppress the auto-save triggers so loading values does not immediately POST them back.

### D2: `IUserApiClient` mirrors the `GameApiClient` result-union pattern

Add `IUserApiClient` with `Task<UserSettingsResult> GetCurrentUserAsync(string accessToken, CancellationToken)` and `Task<SaveSettingsResult> UpdateUserAsync(UserSettings settings, string accessToken, CancellationToken)`. Results are small unions: `UserSettingsResult` ∈ { `Success(UserSettings)`, `NotFound`, `Unauthorized`, `Error` }; `SaveSettingsResult` ∈ { `Success(UserSettings)`, `ValidationFailed`, `NotFound`, `Unauthorized`, `Error` }. Implemented as a typed `HttpClient` (base address = backend URL, 30 s timeout, registered in `MauiProgram`), attaching `Authorization: Bearer`, sending `UpdateUserRequest` JSON on `PUT`, and deserializing `UserDto` → `UserSettings`. Status mapping: `200`→Success, `400`→ValidationFailed (save), `401`→Unauthorized, `404`→NotFound, network/timeout/other→Error — exactly like `GameApiClient`, catching `HttpRequestException`/`TaskCanceledException`.
- *Client model:* `UserSettings(string DisplayName, string PreferredLanguage)` — a subset of `UserDto` (callsign/email not needed by this page). On `PUT`, `FirstName`/`LastName` are sent as `null` (the endpoint accepts them nullable and only requires `DisplayName`/`PreferredLanguage`).
- *Rationale:* consistency with the one existing API client; keeps HTTP/status logic out of the view model and unit-testable via `StubHttpMessageHandler`.

### D3: 300 ms display-name debounce via `TimeProvider` + a cancellation token

The `DisplayName` setter cancels any pending save, then schedules one after 300 ms using `TimeProvider.CreateTimer`. When it fires, the trimmed value is checked for non-empty (the backend requires it); an empty/whitespace name is not sent (and surfaces a "name required" hint). Each fired save creates a fresh `CancellationTokenSource`; a superseding keystroke cancels the previous token so only the latest value is persisted and stale responses are discarded. The save sends the **current** display name plus the **current** language. Inject `TimeProvider` so tests advance a `FakeTimeProvider` and assert the single-request / supersede behaviour deterministically.
- *Rationale:* `TimeProvider` is the standard testable-time abstraction; a raw `Task.Delay` would make the debounce untestable without real waits. Mirrors the debounce approach the sibling `maui-playfields-list-page` change uses for its search.
- *Alternative considered:* save only on focus-loss / an explicit Save button. Rejected — the requirement is auto-save while editing, debounced.

### D4: Language toggle switches live, persists locally, then saves — no debounce

Flipping the toggle sets `SelectedLanguage` and, in that setter, immediately: (1) calls `ILocalizationService.SetLanguage(code)` so the UI re-renders live; (2) persists the code via `ILanguageStore`; (3) fires a `PUT /users/me` with the current display name + new language (no debounce — a toggle is one deliberate action). The local switch and persistence happen **before/independently of** the network save so the UI changes instantly and a failed save does not revert the visible language (it surfaces a non-blocking save-error hint; the local preference still wins until the next successful load).
- *Rationale:* language is a two-state, low-frequency action; instant feedback matters more than coalescing. Decoupling local apply from remote save keeps the UI responsive and offline-tolerant.
- *Alternative considered:* apply the language only after the server confirms. Rejected — laggy and fragile on poor networks; the app is the source of truth for presentation.

### D5: Localization service + `Translate` markup extension for runtime switching

Add `ILocalizationService` (singleton) wrapping a `ResourceManager` over `AppResources.resx` (neutral/English) + `AppResources.nl.resx`. It exposes `string this[string key]`, `CultureInfo CurrentCulture`, `void SetLanguage(string code)`, and raises `PropertyChanged` (indexer) when the language changes. A `TranslateExtension : IMarkupExtension` (used as `Text="{loc:Translate Settings_Title}"`) binds each localized element to the service's indexer, so `SetLanguage` re-renders all visible text without recreating pages or restarting.
- *Rationale:* MAUI's default RESX + `CurrentUICulture` approach does not re-render already-built pages; a service + markup extension bound with `PropertyChanged` is the standard pattern for **runtime** language switching. Keeps the switch instant (D4).
- *Alternative considered:* set `Thread.CurrentThread.CurrentUICulture` and rely on static RESX. Rejected — requires page reconstruction to take effect; the toggle must update the current screen live.
- *Alternative considered:* a third-party localization NuGet. Rejected — a thin in-repo service avoids a new dependency and stays testable (`ILocalizationService` is mockable; the concrete service is plain `net`-testable).

### D6: Device-language default and startup application

Add `ILanguageResolver.Resolve()` returning the effective language at startup: the persisted preference from `ILanguageStore` if present; otherwise the **device** language mapped to a supported code — `nl` when the device's two-letter language is Dutch, else `en` (the only two supported codes; unknown → English per `User.DefaultLanguage`). `App` (or `MauiProgram`) calls `ILocalizationService.SetLanguage(resolver.Resolve())` at startup so the app opens in the right language before Settings is ever visited. When `LoadAsync` later learns the server-stored preference, it aligns the app language to it (and persists), so the account's choice wins on a signed-in device.
- *`ILanguageStore`:* a thin seam over MAUI `Preferences` (`GetLanguage()`/`SetLanguage(code)`) so persistence is mockable and the resolver/VM stay testable.
- *Rationale:* "device language is the default" is the requirement; a resolver keeps the precedence rule (persisted > device > English) in one tested place.

### D7: Migrate existing static UI strings to resource keys

Every user-facing string currently hard-coded in the app's pages (`WelcomePage`, `LoginPage`, `HomePage`, `GamePage`, `StartGamePage`, `PlayfieldsPage`, `SettingsPage`) is moved into `AppResources.resx` under a stable key (e.g. `Menu_Settings`, `Settings_DisplayName_Label`) with a Dutch translation in `AppResources.nl.resx`, and the XAML replaced with `{loc:Translate Key}`. This is what makes the app genuinely multi-lingual rather than just storing a preference.
- *Scope control:* keys are grouped by page; visual/tactical treatment (fonts, casing via styles) is unchanged — only the text source changes. Non-translatable brand text (e.g. the wordmark "THE PREY") may stay literal.
- *Rationale:* a language toggle with no translated screens is not a feature; the requirement explicitly includes "making the app multi-lingual".

### D8: Page layout and new styles

`SettingsPage.xaml` is a `Grid`/`VerticalStackLayout`: a tactical page title, a **display name** section (label + `Entry` bound two-way to `DisplayName`, plus a small saving/saved/error hint), and a **language** section (label + a two-state toggle — a `Switch` or a styled segmented `EN`/`NL` control — bound to `IsDutch`/`SelectedLanguage`). An `ActivityIndicator`/overlay binds to `IsBusy`; a load-error view binds to `HasLoadError`. All labels use `{loc:Translate ...}`. New central styles land in `Styles.xaml`: `SettingsFieldLabel`, `SettingsEntry`, `LanguageToggle` (+ active/inactive segment styles if segmented), and `SettingsStatusHint`. All reuse existing `Tp*` tokens; no inline literals on the page.

## Risks / Trade-offs

- **Loading server values re-triggers auto-save.** → A load-in-progress guard flag (D1) suppresses the debounce/toggle save while `LoadAsync` sets `DisplayName`/`SelectedLanguage`.
- **Debounce is easy to get untestable.** → Inject `TimeProvider` (D3) so the 300 ms wait and single-request/supersede behaviour are asserted with a fake clock, no real delays.
- **Language save fails but UI already switched.** → Local apply + persist are decoupled from the remote save (D4); a save error is a non-blocking hint, the local preference still holds, and the next successful load reconciles.
- **Runtime switch doesn't re-render built pages.** → `ILocalizationService` + `TranslateExtension` bound via `PropertyChanged` (D5) updates visible text live; verified on the current screen when toggling.
- **String-migration sweep is large and error-prone (missing keys).** → Migrate page-by-page, keep keys grouped and named by page, and treat a missing key as a visible fallback (return the key or the English value) rather than a crash; a build check/enumeration confirms every `Translate` key exists.
- **Access-token seam may not exist yet.** → Reuse `IAccessTokenProvider`; if the sibling `maui-playfields-list-page` change has not landed, add the identical seam here (a small, tested service), so this change is not blocked.
- **Style-rule violations (inline colors/sizes on the new page).** → All treatment in `Colors.xaml`/`Styles.xaml`; review the page for literals before done, with the `maui-styling-expert`.

## Migration Plan

Additive to the existing app. Steps: (1) add `UserSettings` + the API-client result unions and `IUserApiClient`/`UserApiClient`; (2) ensure `IAccessTokenProvider` exists (reuse or add); (3) add the localization layer — `AppResources.resx`/`.nl.resx`, `ILocalizationService`, `TranslateExtension`, `ILanguageResolver`, `ILanguageStore`; (4) apply the resolved language at startup (`App`/`MauiProgram`); (5) migrate existing pages' static strings to `Translate` keys (D7); (6) add settings-field/toggle/status styles to `Styles.xaml`; (7) build `SettingsViewModel` (load + debounced name save + language toggle) with `TimeProvider`; (8) rebuild `SettingsPage.xaml`/`.cs` (bind the VM, run `LoadAsync` on `OnAppearing`); (9) register the VM, `IUserApiClient` (typed `HttpClient` on the backend base URL), the localization/language services, and `TimeProvider.System` in `MauiProgram`. The `settings` route and the menu button are unchanged. Rollback is reverting the `SettingsPage` edits (the stub returns) and removing the new services and resource files; page-string migration can be reverted per page. No data impact, no backend change.

## Open Questions

- Language toggle control: a MAUI `Switch` (EN ↔ NL) or a styled segmented `EN | NL` control? (Assumed: segmented, matching the tactical two-state look; final treatment with the `maui-styling-expert`.)
- Display-name constraints on the client: enforce a max length / trim rules before saving, or rely on the backend? (Assumed: trim + non-empty guard client-side; length left to the backend.)
- Should the language save and the display-name save ever coalesce into one request when both change quickly, or always send independently? (Assumed: independent — language immediate, name debounced.)
- How aggressively to migrate strings in this change: all pages now, or infrastructure + Settings/Menu now with remaining pages staged? (Assumed: migrate all current app pages so the app is fully bilingual; flag if the sweep should be split.)
- Fallback when a translation key is missing in `nl`: fall back to English text or show the key? (Assumed: fall back to the English/neutral value.)

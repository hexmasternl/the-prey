## 1. Client model & users API client

- [ ] 1.1 Add a client model `UserSettings(string DisplayName, string PreferredLanguage)` in `Services/Api/` mirroring the subset of the backend `UserDto` this page needs
- [ ] 1.2 Add `IUserApiClient` with `GetCurrentUserAsync(string accessToken, CancellationToken)` and `UpdateUserAsync(UserSettings settings, string accessToken, CancellationToken)`, plus result unions: `UserSettingsResult` (`Success(settings)`/`NotFound`/`Unauthorized`/`Error`) and `SaveSettingsResult` (`Success(settings)`/`ValidationFailed`/`NotFound`/`Unauthorized`/`Error`)
- [ ] 1.3 Implement `UserApiClient` as a typed `HttpClient` calling `GET /users/me` and `PUT /users/me`, attaching `Authorization: Bearer`, sending an `UpdateUserRequest` body (`FirstName`/`LastName` = null, `DisplayName`, `PreferredLanguage`), deserializing `UserDto`→`UserSettings`, and mapping `200`→Success, `400`→ValidationFailed (save), `401`→Unauthorized, `404`→NotFound, network/timeout/other→Error — mirroring `GameApiClient` (catch `HttpRequestException`/`TaskCanceledException`)
- [ ] 1.4 Register `IUserApiClient` in `MauiProgram.RegisterServices` as a typed `HttpClient` with the backend base URL (`ThePreyClientOptions.BackendBaseUrl`) and a 30 s timeout

## 2. Access-token provider

- [ ] 2.1 Reuse the `IAccessTokenProvider` seam (`Task<string?> GetAccessTokenAsync(CancellationToken)`); if it does not yet exist in the codebase, add it and its implementation (over `ITokenStore` + `IAuth0TokenClient.RefreshAsync`, in-memory cached, never throws, `null` when no refresh token or the exchange is `Rejected`/`TransientFailure`) and register it as a singleton in `MauiProgram`
- [ ] 2.2 Register `TimeProvider.System` in `MauiProgram` for the debounce (if not already registered)

## 3. Localization infrastructure

- [ ] 3.1 Add resource files `Resources/Strings/AppResources.resx` (neutral/English) and `AppResources.nl.resx` (Dutch), configured for embedded/satellite resource generation
- [ ] 3.2 Add `ILocalizationService` + implementation (singleton) wrapping a `ResourceManager`: `string this[string key]`, `CultureInfo CurrentCulture`, `void SetLanguage(string code)`, raising `PropertyChanged` for the indexer on language change; missing keys fall back to the English/neutral value (never crash)
- [ ] 3.3 Add a `TranslateExtension : IMarkupExtension` (namespace `loc`) that binds a target property to `ILocalizationService`'s indexer for a given key so it re-renders on language change
- [ ] 3.4 Add `ILanguageStore` over MAUI `Preferences` (`GetLanguage()`/`SetLanguage(code)`) and `ILanguageResolver.Resolve()` returning the effective language: stored preference if present, else the device language mapped to `nl` (Dutch) or `en` (otherwise)
- [ ] 3.5 Register `ILocalizationService`, `ILanguageStore`, and `ILanguageResolver` in `MauiProgram`; in `App`/`MauiProgram` startup call `ILocalizationService.SetLanguage(resolver.Resolve())` before the first page shows

## 4. Localize existing UI strings

- [ ] 4.1 Add resource keys (English + Dutch) for all current user-facing strings, grouped by page (e.g. `Menu_*`, `Settings_*`, `Welcome_*`, `Login_*`, `Game_*`, `StartGame_*`, `Playfields_*`)
- [ ] 4.2 Replace hard-coded `Text="..."` literals on `WelcomePage`, `LoginPage`, `HomePage`, `GamePage`, `StartGamePage`, and `PlayfieldsPage` with `{loc:Translate Key}` lookups (leave non-translatable brand text such as the wordmark literal)
- [ ] 4.3 Confirm every `Translate` key used in XAML has a matching entry in `AppResources.resx` (and `.nl.resx`)

## 5. Theme resources

- [ ] 5.1 Add settings-form styles to `Resources/Styles/Styles.xaml`: `SettingsFieldLabel`, `SettingsEntry`, and `SettingsStatusHint` (saving/saved/error hint), no inline literals
- [ ] 5.2 Add a `LanguageToggle` style (plus active/inactive segment styles if a segmented `EN`/`NL` control is used); reuse existing `Tp*` color tokens (add new keys only if strictly needed)

## 6. Settings view model

- [ ] 6.1 Add `SettingsViewModel : ObservableObject` depending on `IUserApiClient`, `IAccessTokenProvider`, `ILocalizationService`, `ILanguageStore`, `TimeProvider`, and a logger; expose `DisplayName`, `SelectedLanguage` (`en`/`nl`, e.g. via `IsDutch`), `IsBusy`, and status flags (`HasLoadError`, `IsSaving`, `HasSaveError`, `DisplayNameRequired`)
- [ ] 6.2 Implement `LoadAsync()` (run on appearing): acquire an access token; if none, show the load-error state; else call `GetCurrentUserAsync` and map Success→populate `DisplayName`/`SelectedLanguage` and align the app language to the loaded preference (persist it), NotFound/Unauthorized/Error→load-error state; toggle `IsBusy` around the call; suppress the auto-save triggers while populating from the server
- [ ] 6.3 Implement the debounced display-name save in the `DisplayName` setter using `TimeProvider`: cancel any pending/in-flight save, wait 300 ms, then if the trimmed value is non-empty call `UpdateUserAsync` with the current name + current language; an empty/whitespace value sends nothing and sets `DisplayNameRequired`
- [ ] 6.4 Ensure a superseding edit cancels the previous save's `CancellationToken` so only the latest value is persisted; map Success→`saved`, ValidationFailed→error hint, Unauthorized→invalidate token + error, Error→error hint; do not lose the edited text on failure
- [ ] 6.5 Implement the language toggle in the `SelectedLanguage`/`IsDutch` setter: immediately call `ILocalizationService.SetLanguage`, persist via `ILanguageStore`, then fire `UpdateUserAsync` with the current name + new language (no debounce); a failed save sets `HasSaveError` without reverting the local language

## 7. Settings page

- [ ] 7.1 Rebuild `Pages/SettingsPage.xaml` as a `Grid`/`VerticalStackLayout`: a tactical page title, a display-name section (label + two-way-bound `Entry` + saving/saved/error hint), and a language section (label + two-state `EN`/`NL` toggle bound to `IsDutch`/`SelectedLanguage`) — all text via `{loc:Translate ...}`, no inline visual literals
- [ ] 7.2 Bind an `ActivityIndicator`/overlay to `IsBusy` and a load-error view to `HasLoadError`
- [ ] 7.3 In `SettingsPage.xaml.cs`, resolve `SettingsViewModel` via DI and run `LoadAsync()` on `OnAppearing`
- [ ] 7.4 Register `SettingsViewModel` in `MauiProgram`; confirm the `settings` route and the main-menu Settings button are unchanged and now land on the real page

## 8. Tests

- [ ] 8.1 Unit-test `UserApiClient` via `StubHttpMessageHandler`: `GET /users/me` 200→Success, 401→Unauthorized, 404→NotFound, network/timeout→Error; `PUT /users/me` 200→Success, 400→ValidationFailed, 401→Unauthorized, 404→NotFound, error→Error; assert the bearer header and the PUT body (`DisplayName`/`PreferredLanguage`) are sent
- [ ] 8.2 Unit-test `LanguageResolver`: stored preference wins; no preference + Dutch device→`nl`; no preference + non-Dutch device→`en`
- [ ] 8.3 Unit-test `LocalizationService`: `SetLanguage` changes the resolved string for a key and raises `PropertyChanged`; a missing key falls back to the neutral value
- [ ] 8.4 Unit-test `SettingsViewModel` load: Success populates name + language and aligns/persists the app language; no token / Unauthorized / NotFound / Error→`HasLoadError`; loading server values does not trigger a save
- [ ] 8.5 Unit-test the debounced name save with `FakeTimeProvider`: rapid edits send a single request for the final value after 300 ms; a blank value sends nothing and sets `DisplayNameRequired`; a newer edit supersedes an older in-flight save; Success→saved, ValidationFailed/Error/Unauthorized→error hint without losing text
- [ ] 8.6 Unit-test the language toggle: flipping it calls `SetLanguage`, persists via `ILanguageStore`, and sends an update with the new language; a failed backend save sets `HasSaveError` without reverting the local language
- [ ] 8.7 Ensure the test project references any new packages needed for `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) if not already available

## 9. Verification

- [ ] 9.1 Build for Android (`dotnet build src/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj -f net10.0-android`) with 0 warnings / 0 errors; run the MAUI unit tests and confirm all pass
- [ ] 9.2 Confirm review of `SettingsPage.xaml` (and the migrated pages) shows no inline color/opacity/size/border/glow literals and all user-facing text uses `Translate` (single-source-of-truth styling + localization); only layout properties remain inline
- [ ] 9.3 Visually confirm on device/emulator (requires a device/emulator): the page loads and pre-fills the current display name and language; editing the name saves once after a brief pause while rapid typing sends only one request; toggling `EN`/`NL` switches the whole app's text live and persists across a restart; a fresh install with no stored preference opens in the device language (Dutch→`nl`, otherwise `en`); a signed-out/expired session shows the error state without crashing; a failed language save keeps the visible language switched

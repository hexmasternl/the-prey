## 1. Infrastructure & Dependencies

- [x] 1.1 Add `Microsoft.Extensions.Http` NuGet package to `ThePrey.Application.App.csproj`
- [x] 1.2 Create `Models/Playfield.cs` with `Id`, `Name`, and any other fields returned by the API
- [x] 1.3 Create `Services/IPlayfieldService.cs` interface with `GetPlayfieldsAsync()` and `DeletePlayfieldAsync(string id)` methods
- [x] 1.4 Create `Services/PlayfieldService.cs` implementing `IPlayfieldService` using a named `HttpClient` and `IAuthService` for bearer token injection
- [x] 1.5 Register `IHttpClientFactory` (via `AddHttpClient`) and `IPlayfieldService` / `PlayfieldService` in `MauiProgram.cs`

## 2. Local Cache

- [x] 2.1 Create `Services/PlayfieldCacheService.cs` that reads/writes `playfields.json` in `FileSystem.AppDataDirectory` using `System.Text.Json`
- [x] 2.2 Register `PlayfieldCacheService` as a singleton in `MauiProgram.cs`

## 3. Playfields Page

- [x] 3.1 Create `PlayfieldsPage.xaml` and `PlayfieldsPage.xaml.cs` with a `CollectionView` using `SwipeView` item templates and a "Create new" toolbar button
- [x] 3.2 Implement `OnAppearing` in `PlayfieldsPage`: check connectivity, fetch from server (online) or load from cache (offline), update the displayed list
- [x] 3.3 Implement the "Create new" button handler to navigate to the (placeholder) create page route
- [x] 3.4 Implement item tap handler to navigate to the (placeholder) detail page route, passing the selected playfield id
- [x] 3.5 Implement swipe-to-delete: show `DisplayAlert` confirmation; on confirm call `IPlayfieldService.DeletePlayfieldAsync`, remove from cache and list; on API error show error alert and leave item in place
- [x] 3.6 Handle 401 responses in `PlayfieldService` by throwing a typed exception; catch in `PlayfieldsPage` and navigate to login route

## 4. Navigation & Shell Registration

- [x] 4.1 Register `PlayfieldsPage` route (`"playfields"`) in `AppShell.xaml.cs`
- [x] 4.2 Update `MainPage.OnPlayfieldsClicked` to navigate to `"playfields"` instead of showing the "coming soon" alert

## 5. Localization

- [x] 5.1 Add string resources for: page title, "Create new" button, empty-state offline message, delete confirmation prompt, delete error message (both `AppResources.resx` and `AppResources.nl.resx`)

## 6. Verification

- [ ] 6.1 Run the app; verify playfields load from server when online and list updates correctly
- [ ] 6.2 Disable network and reopen playfields page; verify cached playfields are shown
- [ ] 6.3 Swipe a playfield, cancel deletion; verify item remains
- [ ] 6.4 Swipe a playfield, confirm deletion; verify item disappears from list and is removed from server
- [ ] 6.5 Tap "Create new"; verify navigation fires (placeholder page or alert is acceptable for now)
- [ ] 6.6 Tap a playfield; verify navigation fires with the correct playfield id

## ADDED Requirements

### Requirement: Service lifecycle managed by the app
The system SHALL provide an `IGameEngineService` interface and a `GameEngineService` singleton implementation registered in `MauiProgram.cs`. The service SHALL expose `StartAsync(string gameId, PlayerRole role)` and `StopAsync()` methods to begin and end the game loop.

#### Scenario: Service starts successfully
- **WHEN** `StartAsync` is called with a valid `gameId` and `PlayerRole`
- **THEN** the service begins the location-push loop and the game-state-sync loop
- **AND** `GameStateContext.IsRunning` is set to `true`

#### Scenario: Service stops cleanly
- **WHEN** `StopAsync` is called
- **THEN** both loops are cancelled and drained before the method returns
- **AND** `GameStateContext.IsRunning` is set to `false`

#### Scenario: Starting an already-running service is idempotent
- **WHEN** `StartAsync` is called while the service is already running
- **THEN** the existing loops are not duplicated and no exception is thrown

### Requirement: Game loop pauses when app is backgrounded
The service SHALL suspend the active game loops when the app moves to the background and restart them on foreground while a game session is active. (.NET MAUI exposes no `Application.Current.Paused`/`Resumed` events; the cross-platform equivalents `Window.Stopped` and `Window.Resumed` are wired to `IGameEngineService.SuspendAsync()`/`ResumeAsync()` in `App.CreateWindow`.)

#### Scenario: App moves to background
- **WHEN** the OS sends an app-pause lifecycle event
- **THEN** location push and state sync loops are suspended within 2 seconds

#### Scenario: App returns to foreground
- **WHEN** the OS sends an app-resume lifecycle event and a game session is active
- **THEN** location push and state sync loops restart immediately

### Requirement: GameStateContext exposes observable state
The system SHALL provide a `GameStateContext` singleton registered in DI. It SHALL implement `INotifyPropertyChanged`. Pages and controls SHALL obtain it via dependency injection, not via static access.

#### Scenario: Page binds to game state
- **WHEN** a page injects `GameStateContext` and binds to its properties
- **THEN** the page receives UI updates whenever the service updates the context

#### Scenario: Property updates are marshalled to the UI thread
- **WHEN** the background loop writes a new value to a `GameStateContext` property
- **THEN** the property setter dispatches the change notification on the main thread via `MainThread.BeginInvokeOnMainThread`

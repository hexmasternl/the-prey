# Fact Sheet: The Prey Android App (for the website copy)

Every claim the website makes about the game must be traceable to a line in this file. Paths are repo-relative.
`strings` = `app/src/app/src/main/res/values/strings.xml` (EN) and `values-nl/strings.xml` (NL).

## Platform and account

| Fact | Source |
|---|---|
| The only client is a native Android app (Kotlin). No iOS, no web client. | `CLAUDE.md`, `app/src` |
| Installed from Google Play; currently in closed testing. | `website/src/content/en/become-a-tester.md` |
| Sign in with "Sign in" / "Inloggen" on the landing screen (Auth0). An account needs an email address. | strings `landing_sign_in`, `error_sign_in_no_email` |
| Main menu: "Play now" / "Nu spelen" (or "Resume game" / "Spel hervatten" in red when you are in a running game), "Playfields" / "Speelvelden", "Settings" / "Instellingen", "Sign out" / "Uitloggen". | strings `main_menu_*`, `ui/mainmenu/PlayAction.kt` |
| Play now is disabled until precise location is allowed "All the time" and the active-game check succeeded. | `ui/mainmenu/PlayAction.kt` |

## Settings

| Fact | Source |
|---|---|
| Callsign / Roepnaam: 3 to 30 letters, digits, spaces or `- _ & $ # @`. Other players see it during a game. "Save callsign" / "Roepnaam opslaan". | strings `settings_callsign_*` |
| Language / Taal: System default / Systeemstandaard, English, Nederlands. Also sent to your account. | strings `settings_language_*` |
| Location / Locatie section shows location access (Off, Approximate only, While using the app, All the time) and Notifications; "Grant location access" / "Locatietoegang geven". | strings `settings_location_*` |
| Approximate location is not enough; the game needs precise location, set to "Allow all the time". | strings `settings_location_approximate_explanation`, `settings_location_instruction_all_the_time` |
| "Show hints again" resets the first-time tours (in-flight `app-game-first-time-experience`). | change `app-game-first-time-experience` |

## Location tracking

| Fact | Source |
|---|---|
| Location is collected only during an active game, also in the background / screen off, as a foreground service with a persistent notification ("Game tracking" / "Spelvolging"). Stops when the game ends or you leave. | strings `location_consent_*`, `tracking_*` |
| While tracking, the phone sends its position to the server every 5 seconds. | strings `tracking_notification_text`; `Game.LocationReportingIntervalSeconds = 5` (`server/src/Games/HexMaster.ThePrey.Games/DomainModels/Game.cs`) |
| After a phone restart a notification "Your game is still running" asks you to resume tracking. | strings `resume_notification_*` |
| If no location reaches the server for 5 minutes you become passive; after 7 minutes you are out. Out counts as caught for a prey. | `GameParticipant.TryTransitionByTimeout`, `Game.cs` line ~655 (`AllPreysCaught`) |
| When location is unavailable the app warns: "The server can't see you … you can get penalized or ruled out." | strings `tracking_notification_warning_text` |

## Playfields

| Fact | Source |
|---|---|
| Playfields screen has tabs Private / Privé and Public / Openbaar. Private lists your own; Public is searched with at least 3 characters. | strings `playfields_tab_*`, `playfields_search_hint` |
| Create with + on the Private tab ("Create playfield" / "Speelveld maken"); empty list says "Tap + to draw your first". | strings `playfields_create`, `playfields_empty` |
| Delete your own playfield from the list (swipe to reveal Delete / Verwijderen, confirm). | `ui/playfields/SwipeRevealRow.kt`, strings `playfields_delete*` |
| Details screen: Name / Naam, Public / Openbaar switch, Playfield Area / Speelveldgebied, Save / Opslaan, Cancel / Annuleren. A new playfield needs an area of at least 3 points before it can be saved. | strings `playfield_details_*`, `playfield_create_area_hint` |
| Public names follow "CC, City, Fieldname" (NL: "LC, Plaats, Veldnaam"), e.g. `NL, Amsterdam, Vondelpark`. A name that no longer matches switches the playfield back to private. | strings `playfield_details_public_hint`; existing site copy (validated rule) |
| Area editor: full-screen OpenStreetMap map (OpenFreeMap tiles). Tap map adds a point (joined in order). Tap a point selects it (red border); drag the selected point to move it; tap empty map deselects. Delete button (trash) removes the selected point. Erase All / Alles wissen removes all. Save / Opslaan (needs ≥ 3 points) returns the area to the details; Cancel / Annuleren leaves without changes. Pinch to zoom, drag to pan. | `openspec/changes/app-playfield-details-page/specs/playfield-play-area/spec.md`, `app-playfield-edit-area-allow-delete`, `ui/playfields/area/PlayFieldAreaScreen.kt` |
| The playfield is only stored when you tap Save on the details screen. | same spec, "Save the area to the playfield details" |
| First time drawing a new playfield's area shows a short tour. | change `app-game-first-time-experience` |
| A game is played on a playfield chosen while creating the game (Private or Public). There is no "Start Game" on a playfield. | `ui/playfields/select/*`, `ui/navigation/ThePreyNavHost.kt` |

## Creating a game (Game Creator)

| Fact | Source |
|---|---|
| Play now opens "New game" / "Nieuw spel". Choose a playfield ("Choose a playfield" / "Kies een speelveld"; selection screen "Select playfield", tabs Private / Public), set the settings, tap "Create game" / "Spel aanmaken". | `ThePreyNavHost.kt` (`onPlayNow` → `GAME_CREATE`), strings `game_create_*`, `playfield_select_*` |
| Game duration / Speelduur: 30, 60, 90 min (default 30). | `ui/games/create/GameSettings.kt` |
| Hunter delay / Vertraging jager: 3, 5, 10 min (default 5). Time the preys get before the hunter may move. | same, strings `game_create_hunter_delay_help` |
| Final stage / Eindfase: 3, 5, 10 min (default 10). The last minutes, with faster pings. | same |
| Location ping / Locatie-ping: 2, 3, 5 min (default 3). How often prey positions are shared with the hunter. | same |
| Ping in final stage / Ping in eindfase: 1, 2, 3 min (default 1), never longer than the location ping. | same |
| Lobby / Lobby: Game code / Spelcode (4 characters), Share / Delen (sends an invitation link + code), Game settings / Spelinstellingen (creator can change), Players / Spelers. | strings `lobby_*`, `Game.GameCodeLength = 4` |
| Creator taps a player, then "Make hunter" / "Maak Jager". Exactly one hunter; everyone else is prey. The creator can be hunter or prey. | strings `lobby_players_owner_hint`, `lobby_make_hunter` |
| Every player taps "Set ready" / "Ik ben klaar" (tags Waiting / Wacht → Ready / Klaar). Creator taps "Start game" / "Spel starten". | strings `lobby_set_ready`, `lobby_start`, `lobby_hint_*` |
| Minimum 2 players to start, maximum 16 in a lobby. | `Game.MinimumPlayersToStart = 2`, `Game.MaxLobbySize = 16` |
| Leaving the lobby as creator cancels the game for everyone ("Cancel game" / "Spel annuleren"). Others can leave and rejoin. | strings `lobby_leave_body_owner`, `lobby_leave_body` |

## Joining a game

| Fact | Source |
|---|---|
| You join by tapping the invitation link the creator shared (`https://theprey.nl/games/join/{id}/{code}`). It opens the app (App Link); if you are not signed in you sign in first and the invitation is kept. The app joins and opens the lobby. There is no code-entry screen in the app. | `openspec/changes/app-game-share-friends/specs/game-join-by-code/spec.md`, `deeplink/*` |
| Refusals: invitation no longer valid, game already started, lobby full, already in another game, location needed. | strings `join_*` |

## Playing

| Fact | Source |
|---|---|
| Every time the game screen opens, a safety warning "Watch your surroundings" / "Let op je omgeving" must be acknowledged ("I will be careful" / "Ik let goed op"). | strings `game_warning_*`, `ui/game/GameScreen.kt` |
| Hunter delay overlay: prey "Run. Get away from the hunter before the hunt begins."; hunter "Stay in this exact position. Moving before the countdown ends gives you a penalty." | strings `game_delay_*` |
| Prey positions are shared with the hunter from the start, also during the hunter delay, at the ping interval; in the final stage at the final ping interval. | `Game.cs` (final stage), existing rules copy |
| Prey map shows the hunter and the other preys; tagged preys show as ghosts. Hunter map shows the preys (last known locations), tagged ones as ghosts. | `ui/game/map/MarkerStyle.kt` |
| Prey HUD: role pill "Prey" / "Prooi" (red, alternating "Penalty" / "Straf" during a penalty); proximity pill Safe / Veilig (≥ 250 m), Close / Dichtbij (100–250 m), Warning / Waarschuwing (< 100 m), Unknown / Onbekend; panel with Time left / Resterende tijd, Hunter distance / Afstand Jager, Next broadcast / Volgende uitzending; tap the bottom row to collapse / expand. | `openspec/changes/app-game-hud-prey/specs/game-hud-prey/spec.md`, strings `hud_*` |
| Hunter HUD: red role pill "Hunter" / "Jager"; game-clock pill "Live mm:ss" (green > 10 min left, orange ≤ 10, red ≤ 5) or "Penalty mm:ss" / "Straf mm:ss"; tiles Next ping / Volgende ping, Preys in play / Prooien in het spel, Closest prey / Dichtstbijzijnde prooi (to last known location), Penalised preys / Prooien met straf; red "Tag" / "Taggen" button. | `openspec/changes/app-game-hud-hunter/specs/game-hud-hunter/spec.md`, strings `hud_*` |
| Tag button is locked until the hunter delay is over. It opens "Tag a prey" / "Een prooi taggen" listing preys within 50 m (server-checked); choose one, tap "Tag prey" / "Prooi taggen". If the prey got away (409) the list refreshes. | strings `tag_sheet_*`, `Game.TagRangeMeters = 50`, tag-candidates spec |
| A tagged prey stays in the game as a spectator until it ends. | memory "game-end broadcast & spectator", `MarkerStyle` ghosts |
| First time as prey / hunter a tour explains the HUD. | change `app-game-first-time-experience` |

## Penalties

| Fact | Source |
|---|---|
| Hunter moving more than 25 m from the start position during the hunter delay → 10-minute penalty. | `Game.HunterDelayMovementThresholdMeters = 25`, `Game.HunterDelayPenaltyMinutes = 10` |
| Leaving the playfield → 5-minute boundary penalty, only when boundary penalties are on for the game (off by default; the app has no setting for them). Separate flags for preys and hunter. | `Game.PenaltyDurationMinutes = 5`, `GameConfiguration.EnablePreyBoundaryPenalties` / `EnableHunterBoundaryPenalty` (default false), `GameSettings.kt` ("penalty flags unchanged") |
| While a penalty is active, the player's position is reported every 10 seconds instead of at the ping interval. | `Game.PenaltyReportingIntervalSeconds = 10`, `Penalty.cs` |
| Hunter HUD shows how many preys have a penalty; prey HUD flashes "Penalty". | HUD specs |

## End of the game

| Fact | Source |
|---|---|
| The game ends when every prey is caught (tagged or out) or the duration has elapsed. Nobody can end it early. | `GameSweepProcessor.cs` step 4 |
| Hunter wins if every prey is caught; preys win if at least one prey is still in play when time runs out. | `Game.cs` (`AllPreysCaught`), strings `game_result_*` |
| Result dialog per role: "Hunter won" / "Jager wint", "Hunter lost", "Prey won" / "Prooien winnen", "Prey lost", with "Back to menu" / "Terug naar menu". | strings `game_result_*` |
| Finished games are cleaned up after 48 hours. | `Game.CleanupWindowHours = 48` |

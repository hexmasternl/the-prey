# Play Console — Location Disclosure and Data Safety

Text for the Google Play Console that has to match the in-app location tracking disclosure
(`app/src/app/src/main/res/values/strings.xml` and `values-nl/strings.xml`, keys `location_consent_*`)
and the privacy statement (`website/src/content/{en,nl}/privacy.md`, section *In-Game Location Tracking*).
Change all three together.

## In-app disclosure (reference)

Shown as a modal dialog before any system location prompt (once by itself on the main menu, before
starting or joining a game, and when Grant location access on the Settings screen would show the
prompt). The Settings screen also keeps it as a collapsible warning panel, "Why The Prey uses your
location", below Grant location access. Both use the same string resources.

On Android 10+ a precise grant without background location inside the dialog's flow is followed by an
"Allow all the time" step that opens the app's settings page. Background location is never requested
through a system prompt.

| Key | English | Dutch |
|---|---|---|
| `location_consent_title` | The Prey uses your location | The Prey gebruikt je locatie |
| `location_consent_body` | The Prey tracks your device's precise location only while you are in an active game. | The Prey volgt de precieze locatie van je apparaat alleen terwijl je in een actief spel zit. |
| `location_consent_point_background` | Collection continues while the app is closed or not in use, in the background, or while the screen is off. | Dat gaat door terwijl de app gesloten is of niet wordt gebruikt, op de achtergrond, of terwijl het scherm uit staat. |
| `location_consent_point_notification` | Tracking runs as a foreground service, with a persistent notification for as long as it runs. | De volging draait als voorgrondservice, met een vaste melding zolang die loopt. |
| `location_consent_point_stop` | Tracking stops as soon as the game ends or you leave it. | De volging stopt zodra het spel eindigt of je het spel verlaat. |
| `location_consent_point_purpose` | The game needs your position to show players to the Hunter and to check that you stay inside the playfield. | Het spel heeft je positie nodig om spelers aan de Jager te tonen en om te controleren dat je binnen het speelveld blijft. |
| `location_consent_point_near_me` | When you use Near me on the public playfields, the app sends your approximate position once to find nearby playfields. It is not stored. | Als je In de buurt gebruikt bij de openbare speelvelden, stuurt de app één keer je geschatte positie om speelvelden in de buurt te vinden. Die wordt niet opgeslagen. |
| Actions | Continue / Not now | Doorgaan / Niet nu |

## Location permission declaration

Use this text in the location permissions declaration (and, if the tracking service ends up
needing `ACCESS_BACKGROUND_LOCATION`, in the background location declaration).

**English**

> The Prey is a real-world hide-and-seek game played inside a GPS playfield. While the player is in
> an active game, the app collects the device's precise location, including while the app is closed
> or not in use, in the background, or while the screen is off, because players keep their phone in
> their pocket while they run. The position is used to show players to the Hunter and to check that
> the player stays inside the playfield. Tracking runs as a foreground service with a persistent
> notification for as long as it
> runs, and it stops as soon as the game ends or the player leaves it. Outside an active game the
> app only sends an approximate position (rounded to about 1 km) once, when the player uses Near me to
> find public playfields nearby; it is not stored. The app shows a prominent in-app disclosure with this
> information before it requests the location permission.

**Dutch**

> The Prey is een verstoppertjesspel in de echte wereld, gespeeld binnen een GPS-speelveld. Terwijl
> de speler in een actief spel zit, verzamelt de app de precieze locatie van het apparaat, ook
> terwijl de app gesloten is of niet wordt gebruikt, op de achtergrond, of terwijl het scherm uit
> staat, omdat spelers hun telefoon in hun zak houden terwijl ze rennen. De positie wordt gebruikt om
> spelers aan de Jager te tonen en om te controleren dat de speler binnen het speelveld blijft.
> De volging draait als voorgrondservice met een vaste melding zolang die loopt, en stopt zodra het spel eindigt
> of de speler het spel verlaat. Buiten een actief spel stuurt de app alleen één keer een geschatte
> positie (afgerond op ongeveer 1 km) als de speler In de buurt gebruikt om openbare speelvelden in de
> buurt te vinden; die wordt niet opgeslagen. Voordat de app
> om locatietoestemming vraagt, toont hij een duidelijke melding in de app met deze informatie.

**Video / steps for the reviewer**

1. Install the app and sign in.
2. The disclosure "The Prey uses your location" appears before any system prompt.
3. Tap Continue: the system location prompt follows (and on Android 13+ the notification prompt).
4. Or tap Not now, open Settings from the main menu and tap Grant location access: the same disclosure
   dialog appears first, and only Continue leads to the system location prompt. The collapsible panel
   "Why The Prey uses your location" below the button expands to the same text.
5. Start or join a game: location tracking starts with a persistent notification and stops when the
   game ends.

## Data safety form

| Question | Answer |
|---|---|
| Does the app collect or share user data? | Yes |
| Data type | Location → **Precise location** (and Approximate location) |
| Collected | Yes |
| Shared with third parties | No. Positions go to other players of the same game through our own backend, which Play does not count as sharing with third parties. |
| Processed ephemerally | No. Positions are stored on our servers during the game and permanently deleted within 48 hours after it ends. |
| Required or optional | Optional: the menus work without it, but a game cannot be played without precise location. |
| Purpose | **App functionality** (showing players to the Hunter, playfield boundary checks) |
| Encrypted in transit | Yes |
| Users can request deletion | Yes (privacy@theprey.nl, account removal page) |

**Short explanation (EN):** Precise location is collected only during an active game, also in the
background or with the screen off, to show players to the Hunter and check the playfield boundaries.
A persistent notification is shown while tracking runs. Near me on the public playfields sends an
approximate position once, on request, to find nearby playfields; it is not stored.

**Korte uitleg (NL):** De precieze locatie wordt alleen tijdens een actief spel verzameld, ook op de
achtergrond of met het scherm uit, om spelers aan de Jager te tonen en de grenzen van het speelveld
te controleren. Zolang de volging loopt, is er een vaste melding te zien. In de buurt bij de openbare
speelvelden stuurt op verzoek één keer een geschatte positie om speelvelden in de buurt te vinden; die
wordt niet opgeslagen.

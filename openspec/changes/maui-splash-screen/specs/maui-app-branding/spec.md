## ADDED Requirements

### Requirement: On-brand app icon

The MAUI app SHALL present the brand wolf-in-crosshair mark as its device app icon, on The Prey void-black background (`#0c0e0c`), replacing the .NET template monogram on purple. The icon SHALL be produced from the vector brand mark so it renders crisply at every platform density and adaptive-icon shape.

#### Scenario: Icon on the device home screen

- **WHEN** the app is installed on a device
- **THEN** its home-screen/launcher icon shows the wolf-in-crosshair mark on the void-black background
- **AND** no purple `#512BD4` fill or .NET monogram appears

#### Scenario: Adaptive icon safe area

- **WHEN** the platform renders the icon in a masked shape (e.g. Android adaptive circle/squircle)
- **THEN** the wolf-in-crosshair mark stays within the safe area and is not clipped

### Requirement: On-brand native OS splash

The first-frame splash the OS renders before MAUI finishes loading SHALL show the brand wolf-in-crosshair mark centered on The Prey void-black background (`#0c0e0c`), replacing the .NET template splash on purple.

#### Scenario: Cold-start splash frame

- **WHEN** the app is cold-started
- **THEN** the OS splash shows the brand mark centered on the void-black background
- **AND** the background color of the native splash matches the in-app splash base so there is no color flash on hand-off to the running app

### Requirement: In-app tactical splash on the launch screen

The app's launch screen (`WelcomePage`) SHALL render a "fancy" tactical splash consisting of three stacked layers: a black base at the back, a tactical elevation/operations-grid map image filling the screen behind the content, and the brand logo enlarged in the foreground with a signal-glow. The existing corner-bracket chrome, boot-status readout, and activity indicator SHALL remain visible over these layers.

#### Scenario: Layer composition

- **WHEN** the launch screen is shown during boot
- **THEN** a void-black base fills the screen
- **AND** the tactical elevation map is drawn full-bleed over the base, dimmed so foreground content stays legible
- **AND** the brand logo is drawn in the foreground, larger than the previous template size, with a signal-glow

#### Scenario: Boot content stays legible over the map

- **WHEN** the elevation-map background is present
- **THEN** the logo, wordmark, tagline, corner brackets, status message, and activity indicator all remain clearly readable against the map
- **AND** the boot/routing behavior of the launch screen is unchanged from before this change

#### Scenario: Full-bleed background across form factors

- **WHEN** the launch screen is shown on differing screen sizes and aspect ratios (phone, tablet)
- **THEN** the elevation map covers the entire screen with no letterbox bars or exposed default background

### Requirement: Splash styling centralized, no inline visual properties

The tactical-splash presentation (map-background treatment, enlarged logo, glow, and colors) SHALL be defined in the app's central `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml`. The launch screen markup SHALL consume named styles and color resources and SHALL NOT set colors, sizing, opacity, or glow as inline/local properties.

#### Scenario: No inline visual properties on the launch screen

- **WHEN** the launch-screen XAML is reviewed
- **THEN** its visual treatment (background dimming, logo size, glow, colors) is applied via named styles and `StaticResource` color keys
- **AND** no color, opacity, glow, or size literal is declared inline on the page's visual elements

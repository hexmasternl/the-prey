## ADDED Requirements

### Requirement: Dark surface variant

The app SHALL render the welcome screen using a dark surface by default: hero background `#0C0E0C`, app background `#181B17`, surface `#23271F`, border/line `#39402F`, primary text `#DCF6D2`, secondary text `#8C9A83`, ghost text `#5A6553`. The signal green (`#64FF00`) SHALL be unchanged in both modes.

#### Scenario: Dark mode hero renders

- **WHEN** the device system preference is dark (or unset)
- **THEN** the hero panel background is near-black (`#0C0E0C`) with a dim green diagonal gradient

### Requirement: Light surface variant

The app SHALL override the surface tokens when the device system preference is light (`prefers-color-scheme: light`): hero background `#E8ECE3`, app background `#F5F7F3`, surface `#EAEDE5`, border/line `#C8D0BC`, primary text `#1A1F16`, secondary text `#4A5445`, ghost text `#7A8675`. The signal green and corner bracket colour SHALL remain `#64FF00`.

#### Scenario: Light mode hero renders

- **WHEN** the device system preference is light
- **THEN** the hero panel background is a light warm grey and text is dark, while corner brackets and title highlights remain signal green

### Requirement: Style guide tokens as CSS custom properties

All colour, font-family, and spacing values from the style guide SHALL be defined as CSS custom properties in `src/theme/variables.scss` (e.g., `--tp-signal`, `--tp-bg-void`, `--tp-head`) so that components reference tokens rather than hard-coded hex values.

#### Scenario: Token used in component

- **WHEN** a component sets a colour via a CSS custom property (e.g., `color: var(--tp-signal)`)
- **THEN** the rendered colour matches the style-guide value for the current theme mode

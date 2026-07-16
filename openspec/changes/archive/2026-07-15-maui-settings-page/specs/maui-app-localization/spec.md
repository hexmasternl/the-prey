## ADDED Requirements

### Requirement: App supports English and Dutch

The MAUI app SHALL be presentable in **English** (`en`) and **Dutch** (`nl`). User-facing UI text SHALL be sourced from localized resource strings rather than hard-coded literals, with a resource set for each supported language, so that the app renders in the currently selected language.

#### Scenario: App renders in the selected language

- **WHEN** the app language is set to a supported language
- **THEN** the app's user-facing text is shown in that language

#### Scenario: Missing translation falls back

- **WHEN** a resource string has no translation for the selected language
- **THEN** the app falls back to the English/neutral value rather than showing an empty or broken label

### Requirement: Device language is the default

On first run — when no language preference has been stored — the app SHALL default its language to the **device** language mapped to a supported code: **Dutch** when the device language is Dutch, otherwise **English**.

#### Scenario: Dutch device defaults to Dutch

- **WHEN** the app starts with no stored language preference and the device language is Dutch
- **THEN** the app language is Dutch

#### Scenario: Other device languages default to English

- **WHEN** the app starts with no stored language preference and the device language is not Dutch
- **THEN** the app language is English

### Requirement: Language preference is persisted and applied at startup

The app SHALL persist the selected language locally and SHALL apply it at startup so the app opens in the previously selected language. A stored preference SHALL take precedence over the device language.

#### Scenario: Stored preference wins over device language

- **WHEN** the app starts and a language preference has been stored
- **THEN** the app applies the stored language regardless of the device language

#### Scenario: Preference persists across restarts

- **WHEN** the user selects a language and later restarts the app
- **THEN** the app opens in the previously selected language

### Requirement: Language switches at runtime without restart

Changing the app language while the app is running SHALL update the visible UI text immediately, without requiring a restart or re-navigation.

#### Scenario: Visible text updates on switch

- **WHEN** the app language is changed while a screen is visible
- **THEN** that screen's user-facing text updates to the new language without a restart

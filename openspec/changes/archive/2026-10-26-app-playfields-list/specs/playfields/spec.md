# Spec Delta

## MODIFIED Requirements

### Requirement: List visible play fields

The system SHALL allow an authenticated player to list the play fields they own, both private and public. Public play fields owned by other players are not part of this list; they are discovered through the public play-field search.

#### Scenario: List returns owned and public play fields

- **WHEN** an authenticated player who owns a private and a public play field requests the list of play fields
- **THEN** the system returns both of those play fields, and no play fields owned by other players

#### Scenario: List excludes other players' private play fields

- **WHEN** an authenticated player requests the list of play fields and another player owns a private play field
- **THEN** the returned list does not include that private play field

#### Scenario: List excludes other players' public play fields

- **WHEN** an authenticated player requests the list of play fields and another player owns a public play field
- **THEN** the returned list does not include that public play field

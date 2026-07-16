# maui-playfield-delete Specification

## Purpose
TBD - created by archiving change maui-playfield-remove-existing. Update Purpose after archive.
## Requirements
### Requirement: Swipe-to-reveal delete on the Private list

On the Private tab of the playfields list, each playfield list item SHALL support a left swipe that reveals a **Delete** action button. Revealing the action SHALL NOT by itself delete the playfield. The delete affordance SHALL apply only to the Private list (the user's own playfields), not the Public search results. Its visual treatment SHALL come from the central `Colors.xaml` / `Styles.xaml` resources, not inline literals.

#### Scenario: Swipe reveals the delete action

- **WHEN** the user swipes a Private-list playfield item to the left
- **THEN** a Delete action button is revealed for that item
- **AND** the playfield is not deleted merely by revealing the action

#### Scenario: Public results have no delete action

- **WHEN** the Public tab's search results are shown
- **THEN** the list items expose no delete action

### Requirement: Delete requires confirmation

Tapping the revealed **Delete** action SHALL present a confirmation dialog asking the user to confirm deleting that playfield, with confirm and cancel choices. The delete request SHALL be sent only if the user confirms. If the user cancels (or dismisses) the dialog, the playfield SHALL remain in the list and no request SHALL be sent.

#### Scenario: Confirming proceeds

- **WHEN** the user taps Delete and confirms the dialog
- **THEN** the app sends the delete request for that playfield

#### Scenario: Cancelling aborts

- **WHEN** the user taps Delete and cancels or dismisses the dialog
- **THEN** no delete request is sent
- **AND** the playfield remains in the list unchanged

### Requirement: Confirmed delete calls the backend with the session token

On confirmation, the app SHALL request deletion of the playfield from the backend `DELETE /playfields/{id}` endpoint, attaching the session access token as a bearer credential. When no access token can be acquired, the app SHALL treat the deletion as failed (the item stays in the list) rather than removing the item optimistically.

#### Scenario: Delete request is authenticated

- **WHEN** the user confirms deleting a playfield and an access token is available
- **THEN** the app sends `DELETE /playfields/{id}` for that playfield with the bearer access token

#### Scenario: No access token available

- **WHEN** the user confirms deleting a playfield but no access token can be acquired
- **THEN** no successful deletion occurs
- **AND** the playfield remains in the list
- **AND** an error indication is surfaced

### Requirement: Successful delete removes the item in place

When the backend confirms the deletion succeeded, the app SHALL remove that playfield from the Private list without reloading the whole list. When the backend reports the playfield no longer exists (not found), the app SHALL also remove it from the list, since the desired end state — the playfield gone — is achieved.

#### Scenario: Delete succeeds

- **WHEN** the backend returns success for the delete request
- **THEN** the playfield is removed from the Private list in place
- **AND** the rest of the list is unchanged

#### Scenario: Playfield already gone

- **WHEN** the backend reports the playfield was not found
- **THEN** the playfield is removed from the Private list

### Requirement: Failed delete keeps the item and reports the error

When the delete request fails for a reason other than success/not-found — the token is rejected (unauthorized), the caller is not the owner (forbidden), or the request cannot be completed (network/timeout/unexpected status) — the app SHALL keep the playfield in the list and surface a non-blocking error indication rather than silently removing the item or crashing.

#### Scenario: Unauthorized or forbidden

- **WHEN** the delete request returns unauthorized or forbidden
- **THEN** the playfield remains in the list
- **AND** an error indication is surfaced

#### Scenario: Request cannot complete

- **WHEN** the delete request fails with a network error, timeout, or unexpected status
- **THEN** the playfield remains in the list
- **AND** an error indication is surfaced

### Requirement: Client delete API maps backend status codes

The client playfields API SHALL provide a delete operation over `DELETE /playfields/{id}` that maps the backend responses to a result the view model can act on: success on `204 No Content`, not-found on `404`, forbidden on `403`, unauthorized on `401`, and error on a network failure, timeout, or unexpected status. The operation SHALL NOT throw for these outcomes.

#### Scenario: Status mapping

- **WHEN** the delete operation receives `204`, `404`, `403`, `401`, or a network/timeout/unexpected response
- **THEN** it returns success, not-found, forbidden, unauthorized, or error respectively
- **AND** it does not throw


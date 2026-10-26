# app-update-policy Specification

## Purpose
Defines how a release pull request declares whether the app version it ships is a required update, and how promoting an app version to Google Play production turns that declaration into the minimum supported app version.

## Requirements

### Requirement: Release pull requests declare an update policy
Every pull request into `main` SHALL carry exactly one of the labels `update:optional` or `update:required`. A status check SHALL fail while the pull request has neither label or both, and SHALL re-evaluate whenever labels change.

#### Scenario: No label
- **WHEN** a pull request from `development` into `main` is opened without an update label
- **THEN** the update-policy check fails and names the two allowed labels

#### Scenario: One label
- **WHEN** the pull request carries `update:required` only
- **THEN** the update-policy check passes

#### Scenario: Both labels
- **WHEN** the pull request carries both `update:optional` and `update:required`
- **THEN** the update-policy check fails

#### Scenario: Label changed after opening
- **WHEN** `update:optional` is removed and `update:required` is added to an open pull request
- **THEN** the check runs again and passes

### Requirement: Internal releases never change the minimum version
Publishing an app build to the Play internal testing track SHALL NOT change the minimum supported app version, whatever the update labels of the merged pull requests are.

#### Scenario: Required update published internally
- **WHEN** a pull request labelled `update:required` is merged into `main` and the app is published to the internal testing track
- **THEN** the minimum supported app version is unchanged

### Requirement: Promotion to production is a manual step for one version
A maintainer SHALL be able to promote an app version that is on the Play internal testing track to the production track by starting a workflow with that version. The workflow SHALL refuse a version that is not on the internal track or not higher than the last version promoted to production.

#### Scenario: Promote a version
- **WHEN** the promote workflow is started with `1.4.4` and the internal track holds that version's bundle
- **THEN** that bundle is released on the production track and `1.4.4` is recorded as the last promoted version

#### Scenario: Version not on internal track
- **WHEN** the promote workflow is started with a version whose bundle is not on the internal track
- **THEN** the workflow fails before changing any Play track or the minimum version

#### Scenario: Older version
- **WHEN** the promote workflow is started with `1.4.2` and `1.4.4` was promoted before
- **THEN** the workflow fails before changing any Play track or the minimum version

### Requirement: Promotion decides the update policy from all included pull requests
The promotion SHALL be a required update when at least one pull request merged into `main` between the last promoted version and the promoted version is labelled `update:required`; otherwise it SHALL be optional. A pull request without an update label SHALL count as optional. When no version was promoted before, only the pull request that produced the promoted version SHALL count.

#### Scenario: Required change in an earlier internal release
- **WHEN** `1.4.3` came from a pull request labelled `update:required`, `1.4.4` from one labelled `update:optional`, and `1.4.4` is promoted after `1.4.2`
- **THEN** the promotion is a required update

#### Scenario: Only optional changes
- **WHEN** every pull request in the range is labelled `update:optional`
- **THEN** the promotion is optional and the minimum supported app version is unchanged

#### Scenario: Policy is visible
- **WHEN** the promote workflow decides the policy
- **THEN** the run summary lists the pull requests in the range with their labels and the resulting policy

### Requirement: A required promotion raises the minimum version once the release is live
For a required promotion, the workflow SHALL set the minimum supported app version to the promoted version only after a maintainer has approved that the production release is available in the Play Store. It SHALL NOT lower the minimum version.

#### Scenario: Approval given
- **WHEN** a required promotion of `1.4.4` is approved as live in the store
- **THEN** the minimum supported app version becomes `1.4.4`

#### Scenario: Approval withheld
- **WHEN** a required promotion is never approved
- **THEN** the production release exists but the minimum supported app version is unchanged

#### Scenario: Optional promotion
- **WHEN** an optional promotion completes
- **THEN** no approval is requested and the minimum supported app version is unchanged

### Requirement: Infrastructure deployments keep the minimum version
Deploying the Games infrastructure SHALL NOT change the minimum supported app version.

#### Scenario: Games deploy after a required promotion
- **WHEN** the minimum supported app version is `1.4.4` and the Games infrastructure is deployed again
- **THEN** the minimum supported app version is still `1.4.4`

# Spec Delta

## Purpose

Defines how CI derives one version number from the repository state and how the backend and Android app pipelines turn a successful deployment or store publish into a GitHub tag and release, without duplicating either piece of logic.

## ADDED Requirements

### Requirement: Single version derivation
CI SHALL derive version numbers with GitVersion, configured by the repository's `GitVersion.yml`, in exactly one reusable workflow file. Every workflow that needs a version SHALL obtain it from that file and SHALL NOT run GitVersion itself.

#### Scenario: Backend workflow needs a version
- **WHEN** a backend service workflow (Games, Notifications, PlayFields or Users) runs
- **THEN** it obtains its version from the shared versioning workflow and uses that value for the container image tag and the assembly version

#### Scenario: App workflow needs a version
- **WHEN** the Android app release workflow runs
- **THEN** it obtains its version from the same shared versioning workflow and uses that value as the app's `versionName`

#### Scenario: Same commit, same version
- **WHEN** two workflows run on the same commit
- **THEN** both receive the same version number

### Requirement: Idempotent release creation
CI SHALL provide one reusable step that takes a version number as input, checks whether a GitHub release for that version already exists and creates the tag and release at the current commit only when it does not. The step SHALL be the only place in CI that creates releases.

#### Scenario: Release does not exist yet
- **WHEN** the step runs with version `1.1.8` and no release `v1.1.8` exists
- **THEN** it creates the tag `v1.1.8` at the workflow's commit and a GitHub release titled `v1.1.8` with generated notes

#### Scenario: Release already exists
- **WHEN** the step runs with version `1.1.8` and the release `v1.1.8` already exists
- **THEN** it creates nothing and completes successfully

#### Scenario: Concurrent runs for the same version
- **WHEN** two workflows run the step for the same version at the same time and the other one creates the release first
- **THEN** the losing run detects that the release now exists and completes successfully

#### Scenario: Version with or without prefix
- **WHEN** the step receives `1.1.8` or `v1.1.8`
- **THEN** it uses the tag `v1.1.8` in both cases

#### Scenario: Empty version
- **WHEN** the step receives an empty version
- **THEN** it fails the job without creating a tag or release

#### Scenario: Creation fails for another reason
- **WHEN** creating the release fails and no release for that version exists afterwards
- **THEN** the step fails the job

### Requirement: Backend release after deployment
Each backend service workflow SHALL run the release step with its derived version only after its deployment to Azure has succeeded, and only for pushes to `main`.

#### Scenario: Successful deployment on main
- **WHEN** a push to `main` deploys a backend service successfully
- **THEN** the release step runs with that workflow's derived version

#### Scenario: Failed deployment
- **WHEN** the deployment of a backend service fails
- **THEN** no release is created by that workflow

#### Scenario: Pull request
- **WHEN** a backend service workflow runs for a pull request
- **THEN** no release is created

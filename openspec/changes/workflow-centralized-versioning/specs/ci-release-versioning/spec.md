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

### Requirement: Android app release after publish
CI SHALL provide a release workflow for the Kotlin Android app that builds a signed bundle with the derived version, publishes it to Google Play under `nl.hexmaster.theprey`, and then runs the release step with the same version, only after the Play publish has succeeded.

#### Scenario: Successful publish
- **WHEN** the app workflow publishes the bundle to Google Play successfully
- **THEN** the release step runs with the version that was baked into the bundle

#### Scenario: Failed publish
- **WHEN** the Play publish fails
- **THEN** no release is created by that workflow

#### Scenario: Empty version
- **WHEN** the derived version is empty
- **THEN** the workflow fails before building, rather than shipping the Gradle fallback version

### Requirement: Increasing app version code
The app release workflow SHALL pass a `versionCode` to the build that is derived deterministically from the version number and grows whenever the version grows, so every newer release has a higher code than the one before.

#### Scenario: Newer version
- **WHEN** version `1.2.0` is built after version `1.1.9`
- **THEN** the `versionCode` of `1.2.0` is higher than that of `1.1.9`

#### Scenario: Patch beyond 99
- **WHEN** version `1.1.120` is built after version `1.1.99`
- **THEN** the `versionCode` of `1.1.120` is higher than that of `1.1.99` and lower than that of `1.2.0`

# Tasks

## 1. Shared versioning

- [x] 1.1 Update the header comment of `.github/workflows/version.yml` to state it is the only place GitVersion runs and that `major`/`minor`/`patch` feed the app `versionCode`; verify `grep -rl "gitversion/execute" .github` lists only `version.yml`

## 2. Create-release action

- [x] 2.1 Add `.github/actions/create-release/action.yml` (composite, `shell: bash`) with inputs `version` (required), `target` (default `${{ github.sha }}`), `token` (default `${{ github.token }}`) and outputs `tag`, `created`; inputs reach the script only via `env:`; verify `actionlint` (or `npx @action-validator/cli`) reports no errors
- [x] 2.2 Implement the script: strip a leading `v`/`V`, fail with `::error::` on an empty version, `gh release view` → skip with `created=false`, else `gh release create "$TAG" --target --title "$TAG" --generate-notes`, on failure re-check and treat an existing release as success, otherwise exit 1; verify locally by running the script body with `GH_TOKEN` against the repo using an existing tag (`1.1.7` → skip, `created=false`) and an empty version (→ exit 1)
- [x] 2.3 Add a short usage comment at the top of `action.yml` (checkout required, job needs `contents: write`, example call); verify it matches how the workflows call it

## 3. Backend workflows

- [x] 3.1 In `games.yml`, replace the "Create tag and GitHub release" script with `uses: ./.github/actions/create-release` and `version: ${{ needs.version.outputs.semVer }}`; drop `fetch-depth: 0` from that job's checkout; keep `needs: [version, deploy]`, the `push` condition and `contents: write`; verify with `actionlint`
- [x] 3.2 Apply the same change to `notifications.yml`, `playfields.yml` and `users.yml`; verify `grep -n "gh release" .github/workflows/*.yml` returns nothing
- [ ] 3.3 Verify on a real run: push a change to one backend module on `main` and confirm the Tag & Release job logs the tag and `created=true` (new version) or the skip message (existing version) — **auto: needs human:** needs a real push to `main` and a GitHub Actions run (pushing is out of scope here)

## 5. Documentation

- [x] 5.1 Add a "Releases & versioning" section to `CLAUDE.md` (version from `version.yml` only, releases via the create-release action only, shared `v<semVer>` line, app `versionCode` formula and required secrets); verify the section names the same file paths as the workflows

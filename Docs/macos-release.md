# TokenForge macOS Release Preparation

Phase 33 finalizes the post-readiness publication loop for GitHub Release asset validation, non-publishing publication planning, guarded publication dry-run/actual publish, post-release audit, and status dashboard v2. Developer ID signing, notarization submit/staple/verify, manual Gatekeeper QA completion, final privacy regression audit, evidence lock, release tag creation, GitHub Release publication, and final audit generation remain explicit operator steps. Dry-run checks remain safe without Apple or GitHub credentials and must not claim notarization success, public distribution readiness, or publication.

## Supported Startup Path

The only supported normal runtime UI path is:

```text
Assets/_Project/Scenes/Bootstrap.unity
Assets/_Project/Prefabs/UI/BootstrapRoot.prefab
```

The dev-only script-built bootstrap UI fallback has been removed. If `BootstrapRoot.prefab` is missing, startup logs a safe asset-level error and does not create a replacement script UI.

## Smoke Commands

```bash
scripts/validate-release-metadata.sh
scripts/build-macos-smoke.sh
scripts/run-macos-smoke.sh
scripts/package-macos-smoke.sh
scripts/package-macos-release-candidate.sh
scripts/sign-macos-release-candidate.sh
scripts/notarize-macos-release-candidate.sh
scripts/generate-release-readiness-report.sh
scripts/collect-release-evidence.sh
scripts/gatekeeper-qa-helper.sh
scripts/validate-gatekeeper-qa-status.sh
scripts/update-gatekeeper-qa-status.sh init
scripts/update-gatekeeper-qa-status.sh summary
scripts/update-gatekeeper-qa-status.sh validate
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/generate-release-operator-handoff.sh
scripts/prepare-release-tag.sh
scripts/final-privacy-regression-audit.sh
scripts/validate-release-freeze-manifest.sh
scripts/run-release-machine-finalization.sh
scripts/lock-release-evidence.sh
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/publish-github-release.sh
scripts/generate-post-release-audit.sh
scripts/print-release-status-dashboard.sh
scripts/run-credentialed-release.sh
scripts/smoke-clean-install-macos.sh
scripts/smoke-update-migration-macos.sh
scripts/release-prep-check.sh
```

`scripts/package-macos-smoke.sh` uses `/tmp/tokenforge-release/TokenForge.zip` by default, optionally ad-hoc signs the app, verifies the zip contains `TokenForge.app`, and checks that local-only/generated artifacts are not packaged.

`scripts/package-macos-release-candidate.sh` produces `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip` by default for ad-hoc smoke and `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18-notarized.zip` by default when notarization is enabled. It validates release metadata, signs in ad-hoc or Developer ID mode, verifies hardened runtime/signing, validates `Info.plist`, and rejects forbidden local/generated artifacts. In notarized mode it submits the pre-staple package, staples the accepted app, verifies stapling/codesign/Gatekeeper, re-zips the stapled app, and verifies the extracted app again.

`scripts/validate-release-metadata.sh` runs `TokenForge.Editor.ReleaseMetadataValidator.ValidateForRelease` in Unity batchmode. It does not require Developer ID certificates, Apple ID credentials, notary credentials, live server credentials, Git repositories, Claude logs, Codex logs, or Keychain access.

`scripts/release-prep-check.sh` runs the credential-free release preparation path: release metadata validation, EditMode tests, PlayMode tests, Safe Sync contract bundle export, macOS build smoke, package smoke, release candidate package smoke, signing dry-run readiness, notarization dry-run readiness, readiness report v5 generation, release evidence bundle collection, Gatekeeper QA helper inspection, QA status validation, credentialed verification summary validation, final release-record validation, release operator handoff generation, final privacy regression audit, freeze-manifest validation, release-machine finalization checklist, evidence lock summary, final distribution verification, GitHub Release draft generation, release bundle indexing, GitHub asset validation, publication plan generation, publish dry-run, post-release audit, status dashboard v2 generation, release tag dry-run inspection, clean install smoke, and update migration smoke. `--summary-only` validates existing normalized summaries without rebuilding or overwriting a credentialed candidate.

## Phase 30 MVP Release Freeze

Frozen release-candidate metadata lives in `Docs/release-freeze-manifest-v0.18.0-build18.md`:

- App/platform: TokenForge for macOS.
- Version/build: `0.18.0` / `18`.
- Candidate filename: `TokenForge-macOS-0.18.0-build18.zip`.
- Local candidate path: `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip`.
- Frozen dry-run SHA-256: `24d6c3d30c3a967a350fec56baf010c39b80f8b9ef2be5bb21543dac4b385d46`.
- Current state: `dryRunReady`, `SIGNING_READY_BUT_IDENTITY_MISSING`, `NOTARIZATION_READY_BUT_CREDENTIALS_MISSING`, QA `inProgress`.
- Distribution readiness: dry-run only, not public distribution-ready.

The freeze means the local release candidate metadata, checksum, expected runbook, and privacy assertions are stable enough for release-machine handoff. It does not mean the app is notarized, stapled, Gatekeeper accepted, or ready for public distribution.

Exact dry-run validation sequence:

```bash
scripts/run-credentialed-release.sh
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/generate-release-readiness-report.sh
scripts/generate-release-operator-handoff.sh
scripts/prepare-release-tag.sh
scripts/final-privacy-regression-audit.sh
scripts/release-prep-check.sh --summary-only
```

Exact credentialed release-machine sequence:

```bash
scripts/run-credentialed-release.sh --credentialed
scripts/verify-credentialed-release.sh
scripts/update-gatekeeper-qa-status.sh init
scripts/update-gatekeeper-qa-status.sh summary
scripts/update-gatekeeper-qa-status.sh validate
scripts/generate-final-release-record.sh
scripts/generate-release-readiness-report.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
scripts/release-prep-check.sh --summary-only
scripts/prepare-release-tag.sh
```

Required release-machine environment variable names are `DEVELOPER_ID_APPLICATION`, `APPLE_ID`, `APPLE_TEAM_ID`, and `APPLE_APP_SPECIFIC_PASSWORD`. Only names belong in docs and evidence; values must stay in the release shell.

## Phase 31 Finalization And Evidence Lock

Default release-machine checklist mode:

```bash
scripts/run-release-machine-finalization.sh
```

Credentialed release-machine finalization after credential variables are present:

```bash
scripts/run-release-machine-finalization.sh --credentialed
```

The credentialed mode runs this sequence:

```bash
scripts/release-prep-check.sh
scripts/run-credentialed-release.sh --credentialed
scripts/verify-credentialed-release.sh
scripts/gatekeeper-qa-helper.sh
scripts/update-gatekeeper-qa-status.sh summary
scripts/generate-final-release-record.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
scripts/release-prep-check.sh
```

After manual QA has been completed, run:

```bash
scripts/update-gatekeeper-qa-status.sh summary
scripts/update-gatekeeper-qa-status.sh validate
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
scripts/validate-release-freeze-manifest.sh
scripts/run-release-machine-finalization.sh
scripts/lock-release-evidence.sh
scripts/generate-release-readiness-report.sh
scripts/prepare-release-tag.sh
```

Exact tag sequence after every gate passes:

```bash
scripts/prepare-release-tag.sh
scripts/prepare-release-tag.sh --create --yes

# Optional and explicit only after the local tag exists and all gates still pass:
scripts/prepare-release-tag.sh --push --yes
```

`scripts/prepare-release-tag.sh` prints `TAG_BLOCKED_RELEASE_NOT_READY` and does not create or push a tag unless all of these are true: release-machine finalization is `RELEASE_MACHINE_READY_FOR_TAG`, readiness is `readyForDistribution`, verification is `CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION`, QA is `passed`, final privacy audit is `FINAL_PRIVACY_AUDIT_PASSED`, and evidence lock is `EVIDENCE_LOCK_READY`.

`scripts/validate-release-freeze-manifest.sh` compares the tracked manifest, readiness report, final release record, and current package checksum. Dry-run SHA drift may pass only when the manifest documents regenerated dry-run SHA drift and the release does not claim distribution readiness. A final credentialed `readyForDistribution` state must record the final verified SHA in final evidence.

`scripts/lock-release-evidence.sh` writes `/tmp/tokenforge-release/evidence/evidence-lock-summary.json`. Dry-run state produces `EVIDENCE_LOCK_DRY_RUN_ONLY`, pending QA produces `EVIDENCE_LOCK_PENDING_QA`, blocked or unsafe evidence produces `EVIDENCE_LOCK_BLOCKED`, and final-ready state produces `EVIDENCE_LOCK_READY` plus `/tmp/tokenforge-release/TokenForge-0.18.0-build18-release-evidence.zip` with an archive checksum.

## Phase 32 Final Distribution And GitHub Release Prep

Phase 32 verifies the final publication state after Phase 31 has run on the credentialed release machine:

```bash
scripts/run-release-machine-finalization.sh --credentialed
# Complete every Gatekeeper QA item with scripts/update-gatekeeper-qa-status.sh set <item> passed, then:
scripts/update-gatekeeper-qa-status.sh summary
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/final-privacy-regression-audit.sh
scripts/lock-release-evidence.sh
scripts/generate-release-readiness-report.sh
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/print-release-status-dashboard.sh
scripts/prepare-release-tag.sh --create --yes

# Optional and explicit only:
scripts/prepare-release-tag.sh --push --yes
```

Exact final command order:

1. `scripts/run-release-machine-finalization.sh --credentialed`
2. Complete Gatekeeper QA with `scripts/update-gatekeeper-qa-status.sh set <item> passed` and safe notes where needed.
3. `scripts/verify-credentialed-release.sh`
4. `scripts/generate-final-release-record.sh`
5. `scripts/final-privacy-regression-audit.sh`
6. `scripts/lock-release-evidence.sh`
7. `scripts/generate-release-readiness-report.sh`
8. `scripts/verify-final-distribution-readiness.sh`
9. `scripts/generate-github-release-draft.sh`
10. `scripts/generate-release-bundle-index.sh`
11. `scripts/print-release-status-dashboard.sh`
12. `scripts/prepare-release-tag.sh --create --yes`
13. Optional: `scripts/prepare-release-tag.sh --push --yes`

Dry-run/ad-hoc sequence:

```bash
scripts/run-release-machine-finalization.sh
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/final-privacy-regression-audit.sh
scripts/validate-release-freeze-manifest.sh
scripts/lock-release-evidence.sh
scripts/generate-release-readiness-report.sh
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/print-release-status-dashboard.sh
scripts/prepare-release-tag.sh
```

Final distribution statuses:

## Phase 33 GitHub Release Publication And Audit

After `FINAL_DISTRIBUTION_READY`, use this exact publication sequence:

```bash
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/prepare-release-tag.sh --create --yes

# Optional:
scripts/prepare-release-tag.sh --push --yes

scripts/publish-github-release.sh

# Optional actual publication only after dry-run review:
scripts/publish-github-release.sh --publish --yes

scripts/generate-post-release-audit.sh
scripts/print-release-status-dashboard.sh
scripts/release-prep-check.sh
```

`scripts/validate-github-release-assets.sh` must report `GITHUB_RELEASE_ASSETS_READY` before public upload. Dry-run/ad-hoc states remain `GITHUB_RELEASE_ASSETS_DRY_RUN_ONLY` or pending and cannot become publish-ready. `scripts/generate-github-release-publication-plan.sh` writes suggested `gh release create` and optional evidence upload commands, but never runs them. `scripts/publish-github-release.sh` publishes only with `--publish --yes`; by default it writes `/tmp/tokenforge-release/github-release-publication-summary.json` as a dry-run. Existing releases are not overwritten unless `--update-existing` is explicitly supplied. `scripts/generate-post-release-audit.sh` writes `/tmp/tokenforge-release/evidence/post-release-audit.md` and `.json`; if no actual publication occurred, it says not published.

Upload only:

- `TokenForge-macOS-0.18.0-build18.zip`
- final release notes from `/tmp/tokenforge-release/evidence/github-release-draft.md`
- `TokenForge-0.18.0-build18-release-evidence.zip` only when evidence lock and asset validation are ready

Do not upload local-only sync state, approved-location data, raw logs, credentials, `.env` files, raw server bodies, prompts, responses, source snippets, stack traces, repo names, branch names, commit hashes, or tokens.

Troubleshooting:

- Final distribution not ready: complete credentialed release, QA, evidence lock, readiness, and final verification.
- Asset mismatch: regenerate release notes and bundle index from the current zip until all SHA-256 values match.
- Tag missing: create the guarded tag only with `scripts/prepare-release-tag.sh --create --yes`.
- GitHub release already exists: publish blocks by default; use `--update-existing` only for safe missing assets/notes.
- `gh` CLI missing or auth unavailable: dry-run remains valid; actual publish blocks safely.
- Unsafe evidence detected: remove unsafe evidence, rerun final privacy audit, lock evidence, and validate assets again.
- Publication succeeded but audit missing: run `scripts/generate-post-release-audit.sh`.

- `FINAL_DISTRIBUTION_READY`: Developer ID signing, hardened runtime, notarization, stapling, `spctl`, package privacy scan, final privacy audit, Gatekeeper QA, evidence lock, freeze final match, readiness report, final release record, and SHA agreement all pass.
- `FINAL_DISTRIBUTION_DRY_RUN_ONLY`: local/ad-hoc validation is useful but not public distribution-ready.
- `FINAL_DISTRIBUTION_PENDING_QA`: credentialed artifact gates are complete enough to wait on manual Gatekeeper QA.
- `FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK`: QA and credentialed gates are complete but final evidence has not been locked.
- `FINAL_DISTRIBUTION_BLOCKED`: a gate failed, a SHA mismatch exists, or a summary overclaims readiness.

`scripts/generate-github-release-draft.sh` writes `/tmp/tokenforge-release/evidence/github-release-draft.md` and `.json` with title `TokenForge macOS 0.18.0 build 18`, suggested tag `v0.18.0-build18`, artifact filename, checksum, honest release status, release notes, installation notes only when final-ready, known limitations, and an asset checklist. `scripts/generate-release-bundle-index.sh` writes `/tmp/tokenforge-release/evidence/release-bundle-index.md` and `.json`; it marks artifacts as `requiredForDistribution`, `optionalEvidence`, `internalOnly`, or `missing`. `scripts/print-release-status-dashboard.sh` writes `/tmp/tokenforge-release/release-status-dashboard.json`.

Attachable GitHub Release items are the release candidate zip, SHA-256 checksum, final release record, and the release evidence archive only when `EVIDENCE_LOCK_READY`. Do not attach raw logs, credential values, local-only sync state contents, approved-location paths, raw server bodies, prompts, responses, commands, source snippets, stack traces, repo names, branch names, commit hashes, or tokens. Before actual notarization/stapling/`spctl` success, do not claim notarization, Gatekeeper acceptance, or final distribution readiness.

## Phase 29 Operator Workflow

Dry-run workflow:

```bash
scripts/run-credentialed-release.sh
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/release-prep-check.sh --summary-only
```

Expected dry-run output includes `CRED_RELEASE_DRY_RUN_READY` when local packaging and safe inspections pass. Missing Developer ID or Apple credentials are reported as the reason final distribution still requires the credentialed command.

Credentialed workflow on the release machine:

```bash
scripts/run-credentialed-release.sh --credentialed
scripts/verify-credentialed-release.sh
```

Required environment variables are `DEVELOPER_ID_APPLICATION`, `APPLE_ID`, `APPLE_TEAM_ID`, and `APPLE_APP_SPECIFIC_PASSWORD`. The scripts must not echo these values, write them to repository files, or include them in evidence.

Manual Gatekeeper QA completion:

```bash
scripts/update-gatekeeper-qa-status.sh init
scripts/update-gatekeeper-qa-status.sh set cleanInstall passed
scripts/update-gatekeeper-qa-status.sh set firstLaunch passed
scripts/update-gatekeeper-qa-status.sh note cleanInstall "Clean install completed with expected Gatekeeper behavior."
scripts/update-gatekeeper-qa-status.sh summary
scripts/update-gatekeeper-qa-status.sh validate
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/generate-release-readiness-report.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
```

Repeat `set <item> <status>` as each checklist item is completed. Allowed statuses are `notStarted`, `passed`, `failed`, `blocked`, and `skippedWithReason`. `skippedWithReason` requires a safe note.

Status transitions:

- Dry-run package ready: `CRED_RELEASE_DRY_RUN_READY`, readiness `dryRunReady`.
- Missing credentials in credentialed mode: `CRED_RELEASE_BLOCKED_MISSING_CREDENTIALS`, readiness `blocked`.
- Signing failure: `CRED_RELEASE_BLOCKED_SIGNING_FAILED`, readiness `blocked`.
- Notarization failure or rejection: `CRED_RELEASE_BLOCKED_NOTARIZATION_FAILED`, readiness `blocked`.
- Stapling failure: `CRED_RELEASE_BLOCKED_STAPLING_FAILED`, readiness `blocked`.
- `spctl` failure: `CRED_RELEASE_BLOCKED_SPCTL_FAILED`, readiness `blocked`.
- Successful Developer ID signing, notarization, stapling, `spctl`, and package privacy before manual QA completion: verification `CRED_RELEASE_VERIFIED_PENDING_QA`, readiness `pendingQa`.
- Successful Developer ID signing, notarization, stapling, `spctl`, package privacy, final privacy audit, credentialed verification, and completed QA before evidence lock: readiness `evidencePending`.
- Successful Developer ID signing, notarization, stapling, `spctl`, package privacy, final privacy audit, credentialed verification, completed QA, and evidence lock: readiness `readyForDistribution`.
- Dry-run or ad-hoc verification: `CRED_RELEASE_VERIFY_DRY_RUN_ONLY`, readiness `dryRunReady`.

Schema v5 files:

- `/tmp/tokenforge-release/signing-status-summary.json`
- `/tmp/tokenforge-release/notarization-status-summary.json`
- `/tmp/tokenforge-release/credentialed-release-summary.json`
- `/tmp/tokenforge-release/credentialed-release-verification.json`
- `/tmp/tokenforge-release/release-readiness-report.json`
- `/tmp/tokenforge-release/evidence/final-release-handoff.md`
- `/tmp/tokenforge-release/evidence/final-release-record.md`
- `/tmp/tokenforge-release/evidence/final-release-record.json`
- `/tmp/tokenforge-release/evidence/release-operator-handoff.md`
- `/tmp/tokenforge-release/evidence/release-operator-handoff.json`
- `/tmp/tokenforge-release/evidence/final-privacy-regression-audit.json`
- `/tmp/tokenforge-release/release-machine-finalization-summary.json`
- `/tmp/tokenforge-release/evidence/release-freeze-validation.json`
- `/tmp/tokenforge-release/evidence/evidence-lock-summary.json`

Readiness report schema v5 includes `schemaVersion: 5`, `releaseStatus`, signing, notarization, credentialed release, credentialed verification, release-machine finalization, freeze-manifest validation, evidence lock, evidence, QA summaries, final release record path, release operator handoff path, release candidate path/checksum, generated time, next action, known limitations, and privacy assertions for package contents, evidence safety, raw sync-state exclusion, approved-location path exclusion, and no background sync addition.

Readiness status rules:

- `dryRunReady`: dry-run or ad-hoc only; no completed credentialed signing and notarization.
- `credentialedSigned`: Developer ID signed with hardened runtime, but notarization is incomplete.
- `notarized`: notarization succeeded, but stapling, `spctl`, or QA is incomplete.
- `stapled`: stapling succeeded, but `spctl` or QA is incomplete.
- `spctlVerified`: `spctl` passed, but a later release gate is incomplete.
- `pendingQa`: signing, notarization, stapling, `spctl`, and package privacy passed, but QA is missing or in progress.
- `evidencePending`: all release gates passed, but the final evidence lock is missing.
- `readyForDistribution`: Developer ID signing, notarization, stapling, `spctl`, package privacy scan, final privacy audit, QA, credentialed verification, and evidence lock all passed.
- `blocked`: any required gate failed or an impossible readiness claim was detected.

Attach the evidence directory under `/tmp/tokenforge-release/evidence/` to the release record only after reviewing `final-release-handoff.md`. The evidence bundle is designed to contain summaries and checksums, not raw logs, credentials, local-only sync state contents, approved-location paths, raw server bodies, prompts, responses, source snippets, stack traces, repository names, branch names, commit hashes, or tokens.

Ad-hoc builds can claim package structure, metadata, checksum, and privacy scan readiness only. They cannot claim Developer ID signing, notarization, stapling, `spctl` distribution readiness, or final Gatekeeper acceptance.

After successful Developer ID notarization, the release can claim Developer ID signing, notarization acceptance, stapling validation, `spctl` verification, checksum identity, and safe evidence generation only if the corresponding summaries report success.

The final release record is attachable only when it states the correct distribution readiness: `dry-run only`, `pending QA`, `blocked`, or `ready for distribution`. It must not claim `ready for distribution` unless every release gate has passed.

Release evidence attachment checklist:

- `release-readiness-report.json`
- `signing-status-summary.json`
- `notarization-status-summary.json`
- `credentialed-release-summary.json`
- `credentialed-release-verification.json`
- `gatekeeper-qa-status.json`
- `final-release-record.md`
- `final-release-record.json`
- `release-operator-handoff.md`
- `release-operator-handoff.json`
- `final-privacy-regression-audit.json`
- `release-machine-finalization-summary.json`
- `release-freeze-validation.json`
- `evidence-lock-summary.json`
- `checksums.txt`

MVP release handoff checklist:

- `scripts/release-prep-check.sh --summary-only` passes.
- `scripts/final-privacy-regression-audit.sh` passes.
- `scripts/generate-release-operator-handoff.sh` generates handoff Markdown and JSON.
- `scripts/run-credentialed-release.sh --credentialed` has run on the release machine.
- Gatekeeper QA checklist is completed and validated.
- Final release record is regenerated after QA.
- `scripts/prepare-release-tag.sh` prints `v0.18.0-build18`; creation requires `--create --yes`.
- Evidence is reviewed and attached.

## Failure Troubleshooting

| Failure | Safe operator response |
| --- | --- |
| QA missing | Run `scripts/update-gatekeeper-qa-status.sh init`, complete checklist items, then rerun verification. |
| QA failed | Keep the release blocked until the failed checklist item is corrected and validated. |
| QA unsafe notes | Replace notes with safe status wording; do not include paths, raw JSON, stack traces, prompts, responses, server bodies, source snippets, tokens, or approved-location details. |
| Checksum mismatch | Rebuild or regenerate summaries from the current release candidate; do not attach mismatched evidence. |
| SHA drift | Run `scripts/validate-release-freeze-manifest.sh`; dry-run drift is acceptable only when documented and no final-readiness claim is made. |
| Missing Developer ID identity | Install or unlock the Developer ID Application certificate on the release machine, then rerun credentialed release. |
| Missing Apple credentials | Provide the required environment variables in the release shell only. Do not store them in repo files. |
| Ad-hoc package submitted | Rebuild and sign with Developer ID before submit mode. |
| notarytool unavailable | Install or select an Xcode toolchain that provides `xcrun notarytool`. |
| Signed but not notarized | Keep readiness at `credentialedSigned`; run notarization submit mode on the release machine. |
| Notarization rejected | Review Apple notary details outside the evidence bundle; do not attach raw logs. Fix the package and rerun. |
| Credentialed signing succeeded but notarization failed | Keep readiness at `credentialedSigned` or `blocked`; fix notarization and rerun finalization. |
| Notarized but not stapled | Keep readiness at `notarized`; staple and validate before proceeding. |
| Stapling failed | Keep the package blocked; rerun only after notarization acceptance is confirmed. |
| spctl failed | Keep the package blocked; do not claim distribution readiness. |
| Notarization succeeded but Gatekeeper/spctl failed | Keep readiness blocked; fix signing/stapling/quarantine assessment inputs and rerun verification. |
| Privacy scan failed | Remove the disallowed artifact source and rebuild. |
| Local-only file found in package | Fix packaging exclusions; local-only files must remain outside the app package. |
| Evidence missing | Regenerate evidence and final handoff before verification can pass. |
| Evidence lock missing | Run `scripts/lock-release-evidence.sh`; readiness must be `evidencePending` or `blocked`, not `readyForDistribution`. |
| Evidence unsafe content detected | Treat the release as blocked, remove unsafe evidence, regenerate normalized summaries, and rerun the final privacy audit. |
| readyForDistribution overclaim | Regenerate readiness after all gates pass; any ad-hoc, QA-pending, audit-failed, or evidence-unlocked state must not be final-ready. |
| Tag blocked | Do not create or push tags; resolve the blocked gate and rerun `scripts/prepare-release-tag.sh`. |
| Unsafe QA notes | Replace notes with status-only, privacy-safe wording and rerun QA status validation. |

## Signing

Ad-hoc local smoke:

```bash
TOKENFORGE_MACOS_SIGN_MODE=adhoc \
TOKENFORGE_MACOS_SIGN_IDENTITY="-" \
scripts/sign-macos-app.sh
```

Developer ID release template. Set the signing identity in the release shell; values are intentionally omitted from documentation:

```bash
scripts/sign-macos-app.sh
```

Verify signing:

```bash
TOKENFORGE_MACOS_BUNDLE_ID="com.tokenforge.client" \
APP_PATH="/tmp/tokenforge-macos-build/TokenForge.app" \
scripts/verify-macos-signing.sh
```

Release-candidate Developer ID dry-run:

```bash
scripts/sign-macos-release-candidate.sh
```

Credentialed release-candidate signing, run only on a release machine after setting `DEVELOPER_ID_APPLICATION` in the shell:

```bash
scripts/sign-macos-release-candidate.sh --sign
```

The release-candidate signing script inspects `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip`, validates bundle metadata, detects existing signing, hardened runtime, entitlements, and Developer ID identity availability, and writes `/tmp/tokenforge-release/signing-status-summary.json`. Without `--sign`, missing Developer ID identity is a dry-run status, not a local validation failure. With `--sign`, the script requires `DEVELOPER_ID_APPLICATION`, signs with hardened runtime and timestamp, verifies with `codesign`, assesses with `spctl` when available, and re-packages the signed app into the release candidate zip. It must not print secrets or write credentials to repository files.

Phase 27 release-machine environment variables:

- `DEVELOPER_ID_APPLICATION`: Developer ID Application certificate identity name.
- `APPLE_ID`: Apple Developer account email for notary submission.
- `APPLE_TEAM_ID`: Apple Developer Team ID.
- `APPLE_APP_SPECIFIC_PASSWORD`: App-specific password for notary submission.

Do not commit these values, print them in logs, or place them in evidence files.

Gatekeeper verification:

```bash
spctl --assess --type execute --verbose /tmp/tokenforge-macos-build/TokenForge.app
```

## Entitlements And Metadata

`BuildSupport/macOS/TokenForge.entitlements` is intentionally minimal and grants no privileged entitlements.

- Keychain: the current non-sandboxed macOS runtime can use Keychain without a committed access-group entitlement.
- Network client access: not declared because sandboxing is not enabled in this phase.
- Hardened runtime: applied by `codesign --options runtime`.
- Product name: `TokenForge`, from `UnityClient/ProjectSettings/ProjectSettings.asset`.
- Company name: `TokenForge`, from `UnityClient/ProjectSettings/ProjectSettings.asset`.
- Bundle identifier: `com.tokenforge.client`, from the Standalone application identifier in `UnityClient/ProjectSettings/ProjectSettings.asset`.
- App version: `0.18.0`, from `bundleVersion`.
- Build number: `18`, from the Standalone build number.
- Startup scene: `Assets/_Project/Scenes/Bootstrap.unity`, enabled in Unity build settings.
- App icon: Standalone PlayerSettings references `Assets/_Project/Art/AppIcon/TokenForgeReleaseIcon.png`.
- Icon source/license: original TokenForge release icon art created for this repository. It is project-owned release art and does not use third-party or copyrighted source assets.
- UI polish: the release-candidate prefab UI keeps the Bootstrap prefab path, uses thin binder components, and improves section spacing, typography, panel grouping, disabled-state help, empty states, privacy notice readability, and local-vs-remote Safe Sync wording.
- Accessibility polish: important buttons keep descriptive text, input fields have visible labels, the password field is masked, status messages include text not color-only state, disabled actions have nearby help text, empty states explain next steps, and visible text is checked for sensitive values.

The package smoke check validates the packaged `Info.plist` for `CFBundleIdentifier`, product name, `CFBundleShortVersionString`, `CFBundleVersion`, and executable presence.

Release candidate metadata stays at app version/build `0.18.0` / `18`. Do not change sync `schemaVersion` or persistence schema versions without migration code and tests.

## Notarization

Notarization is opt-in and never runs during normal validation.

Release-candidate dry-run:

```bash
scripts/notarize-macos-release-candidate.sh
```

Credentialed submit, run only on a release machine after setting the required Apple notarization environment variables in the shell:

```bash
scripts/notarize-macos-release-candidate.sh --submit
```

Submit mode requires a Developer ID signed app. Ad-hoc signed builds report `NOTARIZATION_BLOCKED_ADHOC_SIGNED` in submit mode and are documented as not acceptable for distribution notarization. Dry-run mode may still pass local readiness as `NOTARIZATION_READY_BUT_CREDENTIALS_MISSING` so normal validation remains credential-free.

Keychain profile:

```bash
TOKENFORGE_RELEASE_NOTARIZE=true \
NOTARYTOOL_KEYCHAIN_PROFILE="tokenforge-notary" \
PACKAGE_PATH=/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18-notarized.zip \
scripts/notarize-macos-app.sh
```

Direct environment credential mode, with values set in the release shell and intentionally omitted from documentation:

```bash
TOKENFORGE_RELEASE_NOTARIZE=true \
PACKAGE_PATH=/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18-notarized.zip \
scripts/notarize-macos-app.sh
```

The script submits with:

```bash
xcrun notarytool submit "$PACKAGE_PATH" --output-format json --wait --keychain-profile "tokenforge-notary"
```

When direct credentials are used, the same command shape uses `--apple-id`, `--team-id`, and `--password` with redacted values. Script output reports only package path, credential mode, wait/async mode, and safe status: `Submitted`, `Accepted`, `Invalid`, `Failed`, or `Timed out`.

Staple after notarization:

```bash
APP_PATH="/tmp/tokenforge-macos-build/TokenForge.app" \
TOKENFORGE_MACOS_STRICT_SPCTL=true \
scripts/staple-macos-app.sh
```

Explicit stapling and Gatekeeper verification:

```bash
xcrun stapler staple /tmp/tokenforge-macos-build/TokenForge.app
xcrun stapler validate /tmp/tokenforge-macos-build/TokenForge.app
codesign --verify --deep --strict /tmp/tokenforge-macos-build/TokenForge.app
spctl --assess --type execute --verbose /tmp/tokenforge-macos-build/TokenForge.app
```

Full release candidate template on a release machine. Set signing and notary credentials in the release shell; values are intentionally omitted from documentation:

```bash
scripts/package-macos-release-candidate.sh
```

Local ad-hoc release candidate smoke:

```bash
TOKENFORGE_RELEASE_SIGN_MODE=adhoc \
TOKENFORGE_RELEASE_NOTARIZE=false \
scripts/package-macos-release-candidate.sh
```

Gatekeeper expectations:

- Ad-hoc signing is suitable for local package structure smoke only and is expected not to pass full `spctl --assess --type execute`.
- A properly signed, notarized, and stapled Developer ID build should pass `spctl --assess --type execute`.

## Release Environment Variables

- `TOKENFORGE_RELEASE_SIGN_MODE=adhoc|developer-id`
- `TOKENFORGE_RELEASE_NOTARIZE=true|false`
- `TOKENFORGE_MACOS_SIGN_IDENTITY` for Developer ID signing.
- `TOKENFORGE_MACOS_BUNDLE_ID=com.tokenforge.client`
- `TOKENFORGE_MACOS_ENTITLEMENTS_PATH` to override `BuildSupport/macOS/TokenForge.entitlements`.
- `NOTARYTOOL_KEYCHAIN_PROFILE` for preferred notarytool authentication.
- `APPLE_ID`, `TEAM_ID`, and `APP_SPECIFIC_PASSWORD` only when not using a stored notary profile.

Scripts must not print credential values, certificates, request/response bodies, raw sync payloads, raw local paths, token/session contents, or local approved-location paths.

## Phase 24 Conflict Review, Tombstones, And Retries

Safe Sync retry remains user-controlled:

- `Sync Safe Sessions` performs one explicit sync attempt.
- Temporary failures can add a safe local retry queue entry.
- `Process Eligible Retries` processes eligible queue entries once.
- `Cancel Failed Retries`, `Clear Succeeded`, `Pause Pending Retries`, and `Resume Paused Retries` are explicit batch retry actions.
- `Delete Local Session` removes only the selected local aggregate session.
- `Enqueue All Pending Deletes` creates safe delete retry entries from local tombstones.
- `Process Pending Deletes Once` processes pending tombstones once.
- `Clear Resolved Tombstones`, `Cancel Selected Tombstone`, and `Cancel Failed Tombstones` are explicit tombstone actions.
- `Keep Local`, `Keep Remote`, `Mark Resolved`, and `Cancel Resolution` are explicit conflict actions.
- Merge policy preview/apply actions are explicit and confirmation-gated.
- `Force Retry Selected` is explicit and may bypass `nextAttemptAt` only after confirmation.
- `Fetch Remote` detects safe conflicts but never applies remote data automatically.
- No retry starts on app construction, app start, login, signup, session restore, or scene load.
- No background retry timer exists in this phase.
- No background tombstone processing exists in this phase.
- No background conflict processing exists in this phase.
- Duplicate retry clicks are guarded while a retry is in progress.

Local-only files:

- `tokenforge-sync-retry-queue.local.json`
- `tokenforge-sync-conflicts.local.json`
- `tokenforge-sync-tombstones.local.json`
- `tokenforge-sync-conflict-audit.local.json`
- `tokenforge-sync-local-state.local.json`

These files include `schemaVersion`, are gitignored, fail safely on corrupt or unknown future schema content, and are never part of the Safe Sync contract bundle. Queue entries store only safe metadata: operation type, safe IDs, safe error code, attempt counts, timestamps, status, and schema version. Tombstones store only tombstone ID, client session ID, server session ID when known, deletion time, delete source, sync status, and safe error code. Conflict audit entries store only safe conflict IDs, action/policy/status, safe diff field names, safe before/after summaries, queued retry ID, safe warning IDs, and safe message code, trimmed to the latest 200 entries. UPSERT retries rebuild aggregate session DTOs from current local safe sessions via `SafeSyncMapper` and re-run the privacy guard before sending. Raw request JSON, response JSON, approved-location paths, prompts, responses, commands, filenames, repo names, branch names, usernames, snippets, secrets, tokens, raw logs, and raw Git output are not stored or rendered.

Local delete behavior:

- Unsynced local saved sessions are deleted locally only and do not create remote tombstones.
- Synced local saved sessions create a local tombstone for later explicit remote delete processing.
- Local delete does not delete approved locations.
- Local delete does not delete remote sessions immediately.
- Local delete updates pending UPSERT retry entries so deleted client session IDs are not uploaded later.
- DELETE retry entries use server session ID only.

Tombstone behavior:

- `Enqueue All Pending Deletes` creates `DELETE_REMOTE_SESSION` retry entries only for tombstones with a known server session ID.
- `Process Pending Deletes Once` processes a bounded pending tombstone batch.
- Successful server delete marks the tombstone synced.
- `NOT_FOUND` / `HTTP_404` is treated as already applied and marked safely.
- Auth-required errors pause safely.
- Retryable network/server errors leave tombstones pending and queue a safe retry.
- Nonretryable forbidden/privacy errors fail safely.
- Mixed outcomes return safe counts and leave failed or retryable entries local-only for later explicit action.

Retry behavior:

- `Process Eligible Retries` processes only entries whose `nextAttemptAt` is ready.
- `Force Retry Selected` requires explicit confirmation and may bypass `nextAttemptAt` only for the selected safe entry.
- Force retry does not override auth pause; the user must resume authentication first.
- Force retry still blocks unsafe/invalid payloads and permanent failures.
- Not-ready entries are skipped and reported with safe counts.
- Auth-required errors pause entries safely.
- Privacy/validation failures fail permanently.
- Temporary failures remain pending according to retry policy.
- `Cancel Failed Retries`, `Clear Succeeded`, `Pause Pending Retries`, and `Resume Paused Retries` are explicit user actions.

Conflict behavior:

- Conflict summaries are built from safe metadata only.
- Merge preview is read-only and never persists, queues retry, or marks a conflict resolved.
- `Keep Local` queues a safe re-upload for explicit retry and does not send immediately.
- `Keep Remote` validates a safe remote aggregate and applies it locally only when the user explicitly chooses it.
- `PreferHigherConfidence` chooses the side with the higher existing safe confidence bucket and blocks ties or missing/incomparable values.
- `PreferNewerSafeTimestamp` chooses the side with the newer safe day/hour bucket and blocks missing/equal/incomparable timestamps.
- `MergeNonConflictingAggregates` merges only deterministic safe aggregate fields, such as non-conflicting bucket unions, and blocks ambiguous counts or conflicting values.
- `MarkResolvedOnly` records the decision without changing local data or queuing upload.
- If the remote aggregate snapshot is insufficient, `Keep Remote` marks the decision only and reports a marker-only safe result.
- Unsafe remote aggregates, unsupported schema versions, and invalid safe bucket values are rejected without writing local save data.
- `Mark Resolved` records a safe resolved status without data changes.
- `Cancel Resolution` leaves the conflict unresolved.
- Fetch-time detection creates `RemoteDifferent`, `LocalMissing`, and expected `RemoteMissing` conflicts without duplicate unresolved conflicts for the same pair.

The Safe Sync panel includes retry, conflict, conflict history, and delete/tombstone status summaries plus explicit action buttons. Conflict review shows labeled local and remote safe summaries, detection time, merge policy labels, safe diff field names, recent audit action/policy/status, safe warning IDs, and safe action explanations. It never renders raw JSON, raw paths, approved-location data, prompts, responses, commands, filenames, repo names, branch names, usernames, source snippets, secrets, tokens, raw logs, raw Git output, or raw sync payloads.

Known limitations:

- Conflict merge remains aggregate-only.
- Raw data recovery is not supported because raw data is never synced.
- Server-side merge is not claimed.
- Local delete is explicit.
- Remote delete processing is explicit.
- No background sync, retry, tombstone, or conflict processing exists.
- Safe Sync schemaVersion remains 1.
- Persistence schema remains v1.
- Live server tests are opt-in only.
- Developer ID signing and notarization still require release-machine credentials.

## Clean Install And Update Smoke

Clean install smoke:

```bash
scripts/smoke-clean-install-macos.sh
```

The automated portion unzips the release package into a temporary directory, validates `TokenForge.app`, checks executable metadata, and rejects packaged local data, token/session files, recovery files, `.env`, logs with sensitive markers, and raw sync payload markers. Optional launch is gated by `TOKENFORGE_CLEAN_INSTALL_LAUNCH=true` because GUI launch checks are brittle in headless validation.

Manual clean install checklist:

- Download and unzip the release package.
- Launch `TokenForge.app` from a clean location.
- Verify Gatekeeper behavior. Ad-hoc signing is not expected to pass full Gatekeeper assessment; a Developer ID notarized and stapled build should pass.
- Verify the app launches without crash.
- Verify Bootstrap scene appears.
- Verify Account panel appears.
- Verify Local Analysis panel appears.
- Verify Approved Local Locations panel appears.
- Verify Review panel appears.
- Verify Safe Sync panel appears.
- Verify Recent Sessions panel appears.
- Verify Privacy notice appears.
- Verify no login starts automatically.
- Verify no sync starts automatically.
- Verify no analysis starts automatically.
- Verify approved locations are empty on clean install.
- Verify local sessions are empty on clean install.
- Verify login is required only for server sync.
- Verify local analysis controls remain visible while logged out.
- Verify health check is explicit.
- Verify sync/fetch/delete require auth.
- Verify password input is masked.
- Verify no tokens, passwords, raw paths, prompts, responses, commands, filenames, repo names, branch names, usernames, source snippets, secrets, raw payloads, or approved-location raw paths are visible.
- Verify app can quit/reopen.
- Verify no TokenForge logs contain credentials, tokens, raw sync payloads, raw local paths, prompts, responses, commands, or source snippets.

Update migration smoke:

```bash
scripts/smoke-update-migration-macos.sh
```

Fixtures live under `UnityClient/Assets/_Project/Tests/Fixtures/Persistence/`. The smoke runs `Phase19MigrationSmokeTests`, seeds only synthetic test data, verifies missing-schema safe save data and approved-location settings migrate to schemaVersion 1, verifies future schema and corrupt files fail safely with recovery copies, and confirms approved-location raw paths remain local-only and are not mapped into Safe Sync payloads.

Manual packaged-build update migration checklist:

- Seed legacy v0/missing-schema safe save data in an isolated data directory.
- Seed legacy approved-location settings in the isolated data directory.
- Launch the new packaged app against the isolated data directory if runtime data-dir override is supported by the release build.
- Verify migration to schemaVersion 1.
- Verify corrupt files do not crash app.
- Verify unknown future schema fails safely.
- Verify approved-location paths remain local-only.
- Verify migrated safe save data remains aggregate-only.
- Verify no sync starts automatically.
- Verify backup/recovery files are local-only and not packaged.
- Verify app can quit/reopen after migration.
- Verify package does not include seeded local data.
- Verify logs do not expose raw approved-location paths or full save payloads.

Runtime data-dir override status: automated tests and structural smoke use isolated synthetic paths. Packaged GUI data-dir override is not a documented public release feature in this phase, so packaged migration QA remains structural/test-level unless a release-machine operator can launch with an isolated supported runtime data directory.

## Release Artifact Privacy Scan

The release candidate package scan rejects:

- `TokenForgePlaceholderIcon.png`
- `tokenforge-approved-locations.local.json`
- `tokenforge-sync-retry-queue.local.json`
- `tokenforge-sync-conflicts.local.json`
- `tokenforge-sync-tombstones.local.json`
- `tokenforge-sync-conflict-audit.local.json`
- retry backup/recovery files
- local sync-state debug files
- generated contract bundle artifacts
- token/session files
- `.corrupt`, `.unsupported-schema`, and `.unsafe` recovery copies
- local debug logs
- credentials, `.env`, Apple credentials, and notary credentials
- raw sync payload logs
- persistence test fixtures inside the app package
- editor-only test assemblies
- shell/source scripts that are not intended to ship inside the app bundle

It also validates `Info.plist` for expected product name, bundle id, app version, build number, executable presence, no placeholder bundle id, and no dev-only server URL or placeholder metadata.

## Known Limitations

- No background sync.
- Retry queue processing is explicit and one pass per user action.
- Conflict UI is minimal.
- Tombstone/delete sync is foundation-level for service remote deletes; full local delete flow integration remains future work.
- Safe Sync schema remains version 1.
- Developer ID signing and notarization still require release-machine credentials.
- Live server tests remain opt-in only.

Expected release metadata:

- Product name: `TokenForge`
- Bundle identifier: `com.tokenforge.client`
- Version: `0.18.0`
- Build: `18`

## Secrets Policy

Do not commit certificates, provisioning profiles, API keys, Apple ID credentials, app-specific passwords, notary credentials, build outputs, `.dmg`, `.pkg`, `.zip` release artifacts, token/session files, generated local data, or approved-location raw paths.

Build, sign, package, and notarization scripts must not print credentials, tokens, request bodies, response bodies, raw sync payloads, raw local paths, or token/session contents.

## Persistence Migration Policy

Local JSON persistence files carry explicit `schemaVersion` metadata:

- `tokenforge-save.json` stores safe aggregate save data with `schemaVersion` and the legacy-compatible `SaveVersion`.
- `tokenforge-approved-locations.local.json` stores local-only approved paths with `schemaVersion` and the legacy-compatible `Version`.

Missing `schemaVersion` from older local files is treated as a known legacy shape and migrated in memory to the current schema. Known older schema values are normalized to the current shape. Unknown future schema versions fail safely by returning defaults and preserving the original local file when possible. Corrupted JSON does not crash startup; the repository returns defaults and writes a best-effort local recovery copy with a `.corrupt` suffix.

Save writes are atomic through a temporary file and keep a `.bak` backup when replacing an existing file. Recovery files such as `.bak`, `.tmp`, `.corrupt`, `.unsupported-schema`, and `.unsafe` are local-only and ignored by git.

Safe save data remains aggregate-only. The save repository rejects forbidden raw/private fields before writing. Approved-location raw paths are allowed only in `tokenforge-approved-locations.local.json`; they are never mapped into Safe Sync DTOs, save sessions, contract bundles, or package artifacts.

Forbidden persisted/synced fields include raw paths, file paths, file names, repository names, branch names, commands, prompts, responses, raw logs, source text, snippets, usernames, tokens, secrets, API keys, passwords, approved-location fields, and local-only paths.

## Git Ignore Policy

Generated release, build, contract, local data, token/session, notarization, backup, and corruption recovery artifacts are ignored. Source scripts, prefab assets, scene assets, entitlements, release documentation, tests, and Unity `.meta` files are not ignored.

## Manual Release Checklist

- Run `scripts/validate-release-metadata.sh`.
- Run EditMode tests.
- Run PlayMode tests.
- Export the Safe Sync contract bundle.
- Validate the contract bundle against the server.
- Run macOS build smoke.
- Run launch smoke.
- Run package smoke.
- Run release candidate package smoke.
- Run clean install smoke.
- Run update migration smoke.
- Verify the zip contains only the app bundle and no generated local artifacts.
- Sign with Developer ID credentials on a release machine.
- Notarize explicitly.
- Staple and verify.

## Phase 20 Validation Record

Local credential-free validation should run:

- `scripts/validate-release-metadata.sh`
- EditMode tests.
- PlayMode tests.
- Safe Sync contract bundle export.
- Optional server contract validation against `TokenForgeCoreServer`.
- `scripts/build-macos-smoke.sh`
- `scripts/package-macos-smoke.sh`
- `scripts/release-prep-check.sh`
- `scripts/package-macos-release-candidate.sh`
- `scripts/smoke-clean-install-macos.sh`
- `scripts/smoke-update-migration-macos.sh`
- ad-hoc signing smoke through the package script.

Local Phase 20 validation run on 2026-05-15:

- Release metadata validation: passed.
- EditMode tests: 209 passed, 0 failed, 2 skipped.
- PlayMode tests: 21 passed, 0 failed.
- Safe Sync contract bundle export: passed.
- Server contract validation against `TokenForgeCoreServer`: passed.
- macOS build smoke: passed.
- package smoke: passed.
- release prep check: passed.
- release candidate package: `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip`, ad-hoc signed, notarization disabled, privacy scan passed.
- clean install smoke: structural checks passed; GUI checklist remains manual.
- update migration smoke: passed against synthetic fixtures.
- Developer ID signing/notarization: not run locally because `TOKENFORGE_MACOS_SIGN_IDENTITY`, `NOTARYTOOL_KEYCHAIN_PROFILE`, `APPLE_ID`, `TEAM_ID`, and `APP_SPECIFIC_PASSWORD` were unset.

Release-machine credential validation requires configured Developer ID and notary credentials, with values set in the release shell and omitted from documentation:

```bash
scripts/run-credentialed-release.sh --credentialed
scripts/verify-credentialed-release.sh
```

## Phase 21 Validation Record

Local Phase 21 validation run on 2026-05-15:

- Release metadata validation: passed.
- EditMode tests: 209 passed, 0 failed, 2 skipped.
- PlayMode tests: 21 passed, 0 failed.
- Safe Sync contract bundle export: passed.
- Server contract validation: passed.
- macOS build smoke: passed.
- package smoke: passed.
- release prep check: passed.
- Release candidate package: `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip`, ad-hoc signed, notarization disabled, privacy scan passed.
- Developer ID signing: not run on this machine; safe failure confirmed when `TOKENFORGE_MACOS_SIGN_IDENTITY` is unset.
- Notarization: not run on this machine; safe failure confirmed when `NOTARYTOOL_KEYCHAIN_PROFILE`, `APPLE_ID`, `TEAM_ID`, and `APP_SPECIFIC_PASSWORD` are unset.
- Stapling and Gatekeeper: not run for a notarized app because notarization was not available. Ad-hoc `spctl` non-acceptance remains expected.
- Clean install smoke: structural package checks passed. Manual clean-install GUI QA was not run in this local pass because no credentialed notarized release package was available for Gatekeeper/GUI verification.
- Update migration smoke: synthetic fixture migration checks remain the supported automated path. Packaged GUI migration QA was not run because a public release-build isolated data-dir override is not documented in this phase.
- Privacy scan: passed for the regenerated ad-hoc release candidate package.

Credentialed release-machine Phase 21 command, with credential values set in the release shell and omitted from documentation:

```bash
scripts/package-macos-release-candidate.sh
```

Alternative credential mode also keeps values out of documentation:

```bash
scripts/package-macos-release-candidate.sh
```

## Current Limitations

- Ad-hoc builds are not notarized and will not satisfy full Gatekeeper distribution requirements.
- Prepared for Developer ID signing/notarization, but not notarized unless credentials are provided and `--sign`/`--submit` are explicitly used.
- Developer ID signing/notarization only runs when credentials are provided.
- Persistence schemaVersion 1 only; migration handles missing/known older local shapes and rejects unknown future schemas safely.
- Safe Sync schemaVersion 1 only.
- No background sync.
- Conflict resolution is aggregate-only and remote raw data cannot be recovered.
- Tombstone and retry processing are explicit user actions only.
- No background retry, tombstone, or conflict processing.
- Live tests opt-in only.
- Final UI art may still need a designer pass if visual standards require more than the basic release-candidate polish in this phase.
- Non-macOS/editor secure token store must be injected explicitly.

## Phase 26 Notarization Readiness

`scripts/notarize-macos-release-candidate.sh` is the release-candidate readiness and notarization entrypoint. Without `--submit`, it performs credential-free checks and must not fail only because Apple credentials are missing. It validates the release candidate zip, bundle metadata, signing status, hardened runtime signal when available, entitlements presence, package privacy rejection, and local-only Safe Sync file absence.

Dry-run status values:

- `READY_FOR_CREDENTIALLED_NOTARIZATION`
- `READY_BUT_CREDENTIALS_MISSING`
- `BLOCKED_MISSING_RELEASE_CANDIDATE`
- `BLOCKED_UNSIGNED_APP`
- `BLOCKED_PRIVACY_SCAN_FAILED`
- `BLOCKED_METADATA_INVALID`

Credentialed notarization is opt-in, with credential values set in the release shell and omitted from documentation:

```bash
scripts/notarize-macos-release-candidate.sh --submit
```

The submit path uses `xcrun notarytool submit --wait`, staples only after acceptance, verifies with `spctl` where possible, and reports `NOTARIZATION_SUCCEEDED`, `NOTARIZATION_FAILED`, `STAPLE_FAILED`, or `SPCTL_FAILED`. It must not echo passwords or write credentials to repository files. Ad-hoc signing remains the local fallback and must not be described as notarized.

## Release Readiness Report

`scripts/generate-release-readiness-report.sh` writes `/tmp/tokenforge-release/release-readiness-report.json`. Phase 27 updates the schema to `schemaVersion: 2` with these stable fields: `signingStatus`, `signingIdentityType`, `hardenedRuntime`, `notarizationStatus`, `staplingStatus`, `spctlStatus`, `releaseCandidateSha256`, `evidenceBundlePath`, `gatekeeperQaStatus`, and `manualQaItems`.

The report excludes secrets and raw user paths. It is generated outside the package and is not included in the release zip.

## Phase 27 Release Evidence

`scripts/collect-release-evidence.sh` creates `/tmp/tokenforge-release/evidence/` with:

- `release-evidence.json`
- `release-evidence.md`
- `checksums.txt`
- `gatekeeper-qa-summary.json`
- optional `gatekeeper-qa-status.json`

The evidence bundle includes only safe summaries, controlled `/tmp/tokenforge-release` paths, package SHA-256, signing/notarization/stapling/spctl status, package privacy scan status, validation command names, app version/build metadata, and clean-install QA checklist status. It must not include raw logs, local sync state contents, approved-location paths, credentials, raw paths outside controlled release paths, prompts, responses, source snippets, stack traces, repo names, branch names, commit hashes, or raw Git output.

## Phase 27 Gatekeeper QA Helper

`scripts/gatekeeper-qa-helper.sh` is a developer QA helper. It may inspect quarantine xattrs, codesign verification, `spctl` assessment, stapling status, app bundle metadata, zip contents, and absence of local-only files from the package. It does not remove quarantine, disable Gatekeeper, bypass system protections, or inspect approved-location contents.

Create or validate the local-only QA status file:

```bash
scripts/gatekeeper-qa-helper.sh --init-status
scripts/gatekeeper-qa-helper.sh --set-status=cleanInstall=passed --set-status=privacyScan=passed --notes="Safe QA note"
scripts/gatekeeper-qa-helper.sh --validate-status
```

Allowed checklist values are `notStarted`, `passed`, `failed`, `blocked`, and `skippedWithReason`. Notes are rejected if they include raw paths, secrets, stack traces, server bodies, prompts, responses, source snippets, raw logs, tokens, or approved-location paths.

## Phase 26 Safe Sync Confirmation UX

Dangerous or meaningful Safe Sync actions require an explicit confirmation request before mutation: merge policy apply, Keep Local, Keep Remote, Mark Resolved, selected force retry, retry batch processing or state changes, tombstone batch processing or cancellation, and clearing resolved conflict audit history. High-risk actions require typed confirmation: `MERGE`, `FORCE RETRY`, `CANCEL DELETE`, or `CLEAR HISTORY`.

Confirmation text is built from safe aggregate summaries, safe counts, safe status codes, policy labels, warning IDs/messages, and exact result expectations. Cancel closes the modal without mutation. Wrong typed text does not mutate. Confirmed actions call the existing service methods with explicit confirmation where those APIs require it. Stale or blocked previews produce safe user messages and do not crash.

Clean-install and Gatekeeper QA steps are documented in `Docs/macos-gatekeeper-qa.md`.

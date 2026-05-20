# TokenForge macOS Clean-Install And Gatekeeper QA

Phase 31 keeps credential-free readiness separate from actual Developer ID signing, notarization, manual Gatekeeper QA completion, final privacy audit, evidence lock, release tag creation, and final release-record generation. Ad-hoc builds are useful for local smoke testing, but they are not notarized Developer ID builds and may be blocked by Gatekeeper. Do not present ad-hoc results as notarized release results or final distribution readiness.

## Clean Install

1. Build and package a release candidate with the release scripts.
2. Use a clean macOS user account or a clean test machine when possible.
3. Move `TokenForge.app` from the extracted release candidate into `/Applications`.
4. Launch from Finder and record the expected Gatekeeper result for the signing mode.
5. Confirm no login, sync, fetch, retry, tombstone, or conflict processing starts automatically.
6. Confirm approved locations are empty on a clean install.
7. Confirm local state files are created only after explicit local actions.

## Ad-Hoc Build Expectations

Ad-hoc signed builds are not notarized. Gatekeeper may block first launch after download quarantine is applied. This is expected for local QA and must not be documented as an end-user bypass flow.

Developer QA may inspect status with:

```bash
xattr -p com.apple.quarantine /Applications/TokenForge.app
codesign -dv --verbose=4 /Applications/TokenForge.app
spctl --assess --type execute --verbose=4 /Applications/TokenForge.app
scripts/gatekeeper-qa-helper.sh
```

## Notarized Developer ID Expectations

A credentialed Developer ID build should be signed with a Developer ID Application identity, submitted with `xcrun notarytool submit --wait`, stapled only after acceptance, and verified with `spctl` where available. The release scripts only report notarization success after those commands pass.

Release-machine flow:

```bash
scripts/run-credentialed-release.sh --credentialed
scripts/verify-credentialed-release.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
scripts/validate-release-freeze-manifest.sh
scripts/run-release-machine-finalization.sh
scripts/lock-release-evidence.sh
scripts/generate-release-readiness-report.sh
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/publish-github-release.sh
scripts/generate-post-release-audit.sh
scripts/print-release-status-dashboard.sh
scripts/prepare-release-tag.sh
```

Normal local validation uses dry-run mode and does not require Apple credentials.

## Local-Only State Checks

Safe Sync local-only files must stay outside the packaged zip:

- `tokenforge-sync-retry-queue.local.json`
- `tokenforge-sync-tombstones.local.json`
- `tokenforge-sync-conflicts.local.json`
- `tokenforge-sync-conflict-audit.local.json`
- `tokenforge-sync-local-state.local.json`
- approved-location settings

Conflict audit, retry queue, tombstone state, and approved-location state are local-only. They must not be synced, included in package artifacts, or rendered as raw paths, raw JSON, raw logs, prompts, responses, commands, source snippets, tokens, repo names, branch names, commit hashes, or secrets.

## Package Privacy Verification

Inspect a release candidate without exposing local data:

```bash
zipinfo -1 /tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip | grep -E 'tokenforge-sync|approved-locations|tokenforge-token|tokenforge-session|raw-sync-payload'
scripts/notarize-macos-release-candidate.sh
scripts/generate-release-readiness-report.sh
scripts/collect-release-evidence.sh
scripts/gatekeeper-qa-helper.sh
scripts/verify-credentialed-release.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
```

The first command should print nothing for local-only state. Dry-run readiness should report `SIGNING_READY_BUT_IDENTITY_MISSING` when no Developer ID identity is available and `NOTARIZATION_READY_BUT_CREDENTIALS_MISSING` when Apple credentials are unavailable. Ad-hoc builds must be called out as not acceptable for distribution notarization.

## Release Evidence Bundle

Collect local-only evidence under `/tmp/tokenforge-release/evidence/`:

```bash
scripts/collect-release-evidence.sh
```

Expected files:

- `release-evidence.json`
- `release-evidence.md`
- `final-release-handoff.md`
- `checksums.txt`
- `release-readiness-report.json`
- `signing-status-summary.json`
- `notarization-status-summary.json`
- `credentialed-release-summary.json` when the operator script has run
- `credentialed-release-verification.json`
- `final-release-record.md`
- `final-release-record.json`
- `release-operator-handoff.md`
- `release-operator-handoff.json`
- `final-privacy-regression-audit.json`
- `gatekeeper-qa-summary.json`
- `gatekeeper-qa-status.json` when initialized

Evidence contains summaries and checksums only. It must not include local sync state contents, approved-location paths, raw logs, prompts, responses, commands, source snippets, tokens, raw Git output, repo names, branch names, commit hashes, stack traces, or secrets.

## Checklist Status File

Create and update the local-only QA checklist:

```bash
scripts/update-gatekeeper-qa-status.sh init
scripts/update-gatekeeper-qa-status.sh set cleanInstall passed
scripts/update-gatekeeper-qa-status.sh set firstLaunch passed
scripts/update-gatekeeper-qa-status.sh note cleanInstall "Clean install completed with expected Gatekeeper behavior."
scripts/update-gatekeeper-qa-status.sh summary
scripts/update-gatekeeper-qa-status.sh validate
scripts/validate-gatekeeper-qa-status.sh
```

Allowed values are `notStarted`, `passed`, `failed`, `blocked`, and `skippedWithReason`. Notes must stay safe and must not include raw paths, secrets, stack traces, server bodies, prompts, responses, source snippets, raw logs, tokens, or approved-location paths.

Manual QA completion checklist keys are `cleanInstall`, `firstLaunch`, `moveToApplications`, `quarantineAssessment`, `localStateLocation`, `safeSyncPanelLaunch`, `confirmationModalCancel`, `confirmationModalTypedPhrase`, `conflictAuditLocalOnly`, `retryTombstoneLocalOnly`, `approvedLocationsLocalOnly`, `privacyScan`, and `packageScan`.

After checklist completion, run:

```bash
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/generate-release-readiness-report.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
scripts/validate-release-freeze-manifest.sh
scripts/run-release-machine-finalization.sh
scripts/lock-release-evidence.sh
scripts/generate-release-readiness-report.sh
scripts/prepare-release-tag.sh
```

Use `final-release-handoff.md`, `release-operator-handoff.md`, `final-release-record.md`, and the evidence lock summary as attachable release summaries after verification. Distribution readiness must be one of: dry-run only, pending QA, evidence pending, blocked, or ready for distribution. `ready for distribution` requires Developer ID signing, notarization, stapling, `spctl`, package privacy scan, final privacy audit, credentialed verification, QA, and evidence lock all passing.

## Phase 31 Gatekeeper Completion Flow

Credentialed finalization checklist:

```bash
scripts/run-release-machine-finalization.sh --credentialed
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

Dry-run/ad-hoc state cannot be tagged as a final distribution release. `scripts/prepare-release-tag.sh --create --yes` and `scripts/prepare-release-tag.sh --push` are guarded by `RELEASE_MACHINE_READY_FOR_TAG`, `readyForDistribution`, `CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION`, QA `passed`, `FINAL_PRIVACY_AUDIT_PASSED`, and `EVIDENCE_LOCK_READY`.

Dry-run SHA drift policy: repeated local dry-run packaging may regenerate the zip checksum. This is acceptable only when `scripts/validate-release-freeze-manifest.sh` reports `FREEZE_MANIFEST_VALID_DRY_RUN_DRIFT_DOCUMENTED` and the release remains non-distribution-ready. Final credentialed distribution requires the final SHA in the final release record and evidence lock.

Evidence lock behavior: `scripts/lock-release-evidence.sh` produces only a dry-run summary until release gates pass. It creates `/tmp/tokenforge-release/TokenForge-0.18.0-build18-release-evidence.zip` only after credentialed signing, notarization, stapling, `spctl`, privacy, QA, final audit, and credentialed verification are complete. Unsafe evidence content blocks the lock.

## Phase 32 Final Distribution QA

After Phase 31 evidence is locked, run the final distribution verifier and publication-prep scripts:

```bash
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/print-release-status-dashboard.sh
scripts/prepare-release-tag.sh --create --yes

# Optional and explicit only:
scripts/prepare-release-tag.sh --push --yes
```

`FINAL_DISTRIBUTION_READY` means the artifact is Developer ID signed with hardened runtime, notarized, stapled, `spctl`-passing, package privacy scanned, final privacy audited, QA-passed, evidence-locked, freeze-final-matched, and SHA-consistent across readiness, final record, evidence lock, and the current zip. `FINAL_DISTRIBUTION_DRY_RUN_ONLY` means no public distribution claim is allowed. `FINAL_DISTRIBUTION_PENDING_QA` means finish the checklist in `gatekeeper-qa-status.json`. `FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK` means run the evidence lock and regenerate readiness. `FINAL_DISTRIBUTION_BLOCKED` means stop and fix the failed gate or overclaim.

The GitHub Release draft can be used as a safe staging document. It must say not public distribution-ready when the build is dry-run/ad-hoc, pending QA, pending evidence lock, or blocked. Only a `FINAL_DISTRIBUTION_READY` draft may include final user-facing install notes or claim Developer ID notarization, stapling, `spctl`, Gatekeeper acceptance, and distribution readiness.

## Phase 33 Publication QA

After `FINAL_DISTRIBUTION_READY`, validate upload assets and generate the publication record:

```bash
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/prepare-release-tag.sh --create --yes
scripts/publish-github-release.sh

# Optional actual publish only after dry-run review:
scripts/publish-github-release.sh --publish --yes

scripts/generate-post-release-audit.sh
scripts/print-release-status-dashboard.sh
```

Asset validation checks the release zip filename/checksum, the GitHub Release draft, the release bundle index, the final release record, and the evidence archive when `EVIDENCE_LOCK_READY`. It rejects local-only sync state, approved-location data, raw logs, credentials, `.env` files, and unsafe evidence archive entries. The publish script does not require GitHub credentials in dry-run mode and must not publish unless `--publish --yes` is provided and all final gates still pass.

If publication is blocked, keep the post-release audit as a safe not-published record. If publication succeeds, regenerate the audit and dashboard so the final status includes the publication summary and any safely available GitHub Release URL.

## Phase 31 Handoff Checklist

- Release freeze manifest exists at `Docs/release-freeze-manifest-v0.18.0-build18.md`.
- `scripts/release-prep-check.sh --summary-only` passes.
- `scripts/final-privacy-regression-audit.sh` passes and writes `/tmp/tokenforge-release/evidence/final-privacy-regression-audit.json`.
- `scripts/generate-release-operator-handoff.sh` writes attachable Markdown and JSON handoff files.
- Credentialed release is run on the release machine with `scripts/run-credentialed-release.sh --credentialed`.
- Gatekeeper QA is completed with `scripts/update-gatekeeper-qa-status.sh set <item> <status>`.
- `scripts/generate-final-release-record.sh` is regenerated after credentialed verification and QA.
- `scripts/run-release-machine-finalization.sh` reports `RELEASE_MACHINE_READY_FOR_TAG`.
- `scripts/lock-release-evidence.sh` reports `EVIDENCE_LOCK_READY`.
- `scripts/generate-release-readiness-report.sh` reports schema v5 `readyForDistribution`.
- `scripts/prepare-release-tag.sh` suggests `v0.18.0-build18`; tag creation and push are explicit guarded operator actions.
- Evidence under `/tmp/tokenforge-release/evidence/` is reviewed before attachment.

## Phase 31 Troubleshooting

| Issue | Safe response |
| --- | --- |
| SHA drift | Run `scripts/validate-release-freeze-manifest.sh`; dry-run drift is acceptable only when documented and no final-readiness claim is made. |
| QA missing | Initialize and complete `gatekeeper-qa-status.json`, then rerun verification. |
| QA failed | Keep the release blocked until the failing checklist item is corrected. |
| QA unsafe notes | Replace notes with safe wording only. Do not include paths, raw JSON, stack traces, prompts, responses, server bodies, source snippets, tokens, or approved-location details. |
| Checksum mismatch | Rebuild or regenerate summaries from the current release candidate. |
| Evidence lock missing | Run `scripts/lock-release-evidence.sh`; readiness must be `evidencePending` or `blocked`, not `readyForDistribution`. |
| Evidence unsafe content detected | Remove unsafe evidence, regenerate normalized summaries, rerun final privacy audit, and lock evidence again. |
| readyForDistribution overclaim | Regenerate readiness after all gates pass; dry-run, QA-pending, audit-failed, or unlocked-evidence state must not be final-ready. |
| Tag blocked | Treat `TAG_BLOCKED_RELEASE_NOT_READY` as final; do not create or push tags until all release gates pass. |
| GitHub assets blocked | Regenerate final readiness, draft, bundle index, and evidence lock from the current release candidate; do not upload. |
| GitHub release already exists | Do not overwrite by default; use the guarded `--update-existing` path only for safe missing assets or notes. |
| `gh` CLI missing or auth unavailable | Keep the dry-run summary; actual publication remains blocked without printing or storing tokens. |
| Publication succeeded but audit missing | Run `scripts/generate-post-release-audit.sh` and `scripts/print-release-status-dashboard.sh`. |
| Signed but not notarized | Keep readiness at `credentialedSigned`; run credentialed notarization on the release machine. |
| Credentialed signing succeeded but notarization failed | Keep the release blocked; fix notarization and rerun release-machine finalization. |
| Notarized but not stapled | Keep readiness at `notarized`; staple and validate before proceeding. |
| Stapled but spctl failed | Keep readiness blocked and fix the release candidate. |
| Notarization succeeded but Gatekeeper/spctl failed | Keep readiness blocked; rerun stapling/spctl verification only after the artifact is corrected. |
| Privacy scan failed | Remove the disallowed artifact source and rebuild. |
| Evidence missing | Regenerate the evidence bundle and final handoff before verification can pass. |

## Safe Sync Confirmation QA

Verify these actions open a confirmation modal before mutation:

- merge policy apply
- Keep Local
- Keep Remote
- Mark Resolved
- force retry selected entry
- process eligible retries
- cancel, pause, resume, or clear retry batches
- process pending tombstones
- cancel or clear tombstone batches
- clear resolved conflict audit history

High-risk actions require typed confirmation: `MERGE`, `FORCE RETRY`, `CANCEL DELETE`, or `CLEAR HISTORY`. Cancel must close the modal without mutating state. Wrong typed text must not mutate state. Confirmed actions must show only safe status text.

Phase 27 confirmation QA also verifies stable safe action labels, safe result expectation text, visible typed phrases for high-risk actions, safe wrong-phrase validation text, safe cancel text, safe success/failure text, and safe stale-state text.

## Safe Diagnostics

Collect diagnostics using status codes, counts, signing mode, notarization status, package path under `/tmp/tokenforge-release`, and the generated readiness report. Do not collect raw user paths, raw JSON payloads, prompts, responses, commands, source snippets, tokens, raw logs, raw Git output, repo names, branch names, commit hashes, stack traces, or approved-location paths.

## Known Limitations

- No raw recovery by design.
- No background sync, retry, tombstone, or conflict processing.
- No approved-location sync.
- No server-side merge claim.
- Safe Sync schema remains v1.
- Full Gatekeeper acceptance requires a notarized Developer ID build.

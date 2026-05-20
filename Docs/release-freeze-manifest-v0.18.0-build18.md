# TokenForge MVP Release Freeze Manifest

- App name: TokenForge
- Target platform: macOS
- Release type: MVP release candidate
- Version: 0.18.0
- Build: 18
- Release candidate filename: TokenForge-macOS-0.18.0-build18.zip
- Current local artifact path: /tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip
- Current dry-run SHA-256: 24d6c3d30c3a967a350fec56baf010c39b80f8b9ef2be5bb21543dac4b385d46
- Current regenerated dry-run SHA-256: a4f1b79380a07ddd4aca0845f7c13b5844a2bc147e7bfd621985b8868c69e54a
- Current release status: dryRunReady
- Current signing status: SIGNING_READY_BUT_IDENTITY_MISSING
- Current signing identity type: adHoc
- Current notarization status: NOTARIZATION_READY_BUT_CREDENTIALS_MISSING
- Current stapling status: notAttempted
- Current spctl status: notAttempted
- QA status: inProgress
- Distribution readiness: dry-run only, not ready for public distribution

This manifest freezes the local MVP release-candidate metadata for handoff. The current artifact is an ad-hoc dry-run package. It is useful for local release validation, package privacy scanning, checksum identity, and operator handoff, but it is not notarized and is not ready for public distribution.

## Phase 31 Dry-Run SHA Drift Policy

Dry-run rebuilds may legitimately regenerate zip SHA values before Developer ID signing and notarization. The frozen Phase 29 SHA remains recorded above as the handoff baseline. A regenerated dry-run SHA may pass validation only while the release remains dry-run/ad-hoc and no distribution-ready claim is made. A final credentialed `readyForDistribution` claim requires the final SHA-256 to be recorded in the final release record, release readiness report, and locked evidence summary.

## Exact Release-Machine Finalization Command

```bash
scripts/run-release-machine-finalization.sh --credentialed
```

## Exact Credentialed Release Command

```bash
scripts/run-credentialed-release.sh --credentialed
```

## Exact Post-Credentialed Verification Commands

```bash
scripts/verify-credentialed-release.sh
scripts/update-gatekeeper-qa-status.sh init
scripts/update-gatekeeper-qa-status.sh set cleanInstall passed
scripts/update-gatekeeper-qa-status.sh set firstLaunch passed
scripts/update-gatekeeper-qa-status.sh set moveToApplications passed
scripts/update-gatekeeper-qa-status.sh set quarantineAssessment passed
scripts/update-gatekeeper-qa-status.sh set localStateLocation passed
scripts/update-gatekeeper-qa-status.sh set safeSyncPanelLaunch passed
scripts/update-gatekeeper-qa-status.sh set confirmationModalCancel passed
scripts/update-gatekeeper-qa-status.sh set confirmationModalTypedPhrase passed
scripts/update-gatekeeper-qa-status.sh set conflictAuditLocalOnly passed
scripts/update-gatekeeper-qa-status.sh set retryTombstoneLocalOnly passed
scripts/update-gatekeeper-qa-status.sh set approvedLocationsLocalOnly passed
scripts/update-gatekeeper-qa-status.sh set privacyScan passed
scripts/update-gatekeeper-qa-status.sh set packageScan passed
scripts/update-gatekeeper-qa-status.sh summary
scripts/update-gatekeeper-qa-status.sh validate
scripts/generate-final-release-record.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
scripts/validate-release-freeze-manifest.sh
scripts/run-release-machine-finalization.sh
scripts/lock-release-evidence.sh
scripts/generate-release-readiness-report.sh
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/print-release-status-dashboard.sh
scripts/release-prep-check.sh --summary-only
```

## Exact Final Distribution And Tag Sequence After All Gates Pass

```bash
scripts/run-release-machine-finalization.sh --credentialed
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
scripts/prepare-release-tag.sh
scripts/prepare-release-tag.sh --create --yes

# Optional and explicit only:
scripts/prepare-release-tag.sh --push --yes
```

Tag creation and push are blocked unless final distribution verification is `FINAL_DISTRIBUTION_READY`, release-machine finalization is `RELEASE_MACHINE_READY_FOR_TAG`, readiness schema v5 reports `readyForDistribution`, credentialed verification reports `CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION`, QA is `passed`, final privacy audit is `FINAL_PRIVACY_AUDIT_PASSED`, freeze validation is a final match, and evidence lock is `EVIDENCE_LOCK_READY`.

## Phase 32 Publication Artifacts

Phase 32 adds:

- `/tmp/tokenforge-release/final-distribution-readiness.json`
- `/tmp/tokenforge-release/evidence/github-release-draft.md`
- `/tmp/tokenforge-release/evidence/github-release-draft.json`
- `/tmp/tokenforge-release/evidence/release-bundle-index.md`
- `/tmp/tokenforge-release/evidence/release-bundle-index.json`
- `/tmp/tokenforge-release/release-status-dashboard.json`

Final distribution status meanings:

- `FINAL_DISTRIBUTION_READY`: every final signing, notarization, stapling, `spctl`, privacy, QA, evidence, freeze, record, readiness, and SHA gate passed.
- `FINAL_DISTRIBUTION_DRY_RUN_ONLY`: useful local validation only; no public distribution claim.
- `FINAL_DISTRIBUTION_PENDING_QA`: finish Gatekeeper QA before publication.
- `FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK`: lock evidence and regenerate readiness before publication.
- `FINAL_DISTRIBUTION_BLOCKED`: stop; a gate failed or a summary overclaimed readiness.

GitHub Release attachable items are the final release candidate zip, SHA-256 checksum, final release record, and the release evidence archive only when `EVIDENCE_LOCK_READY`. Do not attach raw logs, credential values, local-only sync state contents, approved-location paths, raw server bodies, prompts, responses, commands, source snippets, stack traces, repo names, branch names, commit hashes, or tokens.

## Phase 33 Publication Closure

Phase 33 adds:

- `/tmp/tokenforge-release/github-release-assets-validation.json`
- `/tmp/tokenforge-release/evidence/github-release-publication-plan.md`
- `/tmp/tokenforge-release/evidence/github-release-publication-plan.json`
- `/tmp/tokenforge-release/github-release-publication-summary.json`
- `/tmp/tokenforge-release/evidence/post-release-audit.md`
- `/tmp/tokenforge-release/evidence/post-release-audit.json`
- release status dashboard v2 at `/tmp/tokenforge-release/release-status-dashboard.json`

After `FINAL_DISTRIBUTION_READY`, run:

```bash
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/prepare-release-tag.sh --create --yes
scripts/publish-github-release.sh
scripts/generate-post-release-audit.sh
scripts/print-release-status-dashboard.sh
scripts/release-prep-check.sh
```

Actual GitHub Release publication is optional and guarded:

```bash
scripts/publish-github-release.sh --publish --yes
```

Only `TokenForge-macOS-0.18.0-build18.zip`, final release notes, and the safe evidence archive `TokenForge-0.18.0-build18-release-evidence.zip` when ready may be uploaded. Do not upload local-only state files, approved-location data, raw logs, credentials, `.env` files, raw server bodies, prompts, responses, source snippets, stack traces, repo names, branch names, commit hashes, or tokens. Dry-run/ad-hoc states must remain not published and not public distribution-ready.

## Required Release-Machine Environment Variables

Set these by name in the release-machine shell only. Do not commit values, print values, or write values to evidence.

- DEVELOPER_ID_APPLICATION
- APPLE_ID
- APPLE_TEAM_ID
- APPLE_APP_SPECIFIC_PASSWORD

## Privacy Assertions

- No local-only sync, audit, retry, tombstone, or conflict files in the package.
- No approved-location paths in evidence.
- No raw sync state in evidence.
- No secrets in evidence.
- No background sync, retry, tombstone, or conflict processing was added.
- Evidence contains normalized summaries and checksums only.
- The release package and evidence must not include raw logs, raw server bodies, prompts, responses, commands, source snippets, repo names, branch names, commit hashes, tokens, or credential values.

## Phase 31 Evidence Lock

`scripts/lock-release-evidence.sh` writes `/tmp/tokenforge-release/evidence/evidence-lock-summary.json`. Dry-run/ad-hoc state produces `EVIDENCE_LOCK_DRY_RUN_ONLY`. Credentialed signing/notarization with incomplete QA produces `EVIDENCE_LOCK_PENDING_QA`. Blocked gates or unsafe evidence produce `EVIDENCE_LOCK_BLOCKED`. Only completed Developer ID signing, notarization, stapling, `spctl`, package privacy scan, final privacy audit, credentialed verification, QA, and safe evidence produce `EVIDENCE_LOCK_READY` and `/tmp/tokenforge-release/TokenForge-0.18.0-build18-release-evidence.zip`.

Readiness report schema v5 adds release-machine finalization, freeze-manifest validation, evidence lock summary, release operator handoff path, and `evidencePending`. `readyForDistribution` is impossible without `EVIDENCE_LOCK_READY`.

## Known Limitations

- Real Developer ID signing was not attempted on this machine.
- Apple notarization was not attempted on this machine.
- Stapling was not attempted because notarization has not succeeded.
- Gatekeeper distribution acceptance has not been proven for the ad-hoc package.
- Manual Gatekeeper QA is still in progress.
- Distribution readiness remains dry-run only until Developer ID signing, notarization, stapling, spctl assessment, package privacy scan, final privacy audit, final release record, and manual QA all pass on the release machine.

## Release Operator Notes

- Treat this manifest as the source of truth for the frozen local release candidate metadata.
- Recompute and compare SHA-256 after credentialed signing and notarization because the release machine may re-package the candidate.
- Run the credentialed command only on a configured release machine.
- Do not tell users to bypass Gatekeeper.
- Do not attach raw logs or private local data to a release issue or release record.
- If any generated summary claims `readyForDistribution` before every required release gate passes, block the release and regenerate the summaries after fixing the gate state.
- If `scripts/prepare-release-tag.sh` prints `TAG_BLOCKED_RELEASE_NOT_READY`, do not create or push a tag.

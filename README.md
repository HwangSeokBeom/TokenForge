# TokenForge

TokenForge is a Unity 2D macOS client for turning local AI coding-agent and Git activity into a privacy-first companion RPG growth loop.

## Client Responsibility

This repository owns only the Unity/macOS client foundation:

- Local project folder connection.
- Git working tree analysis.
- AI agent log Provider architecture.
- Privacy-safe `AgentWorkSession` normalization.
- Local growth calculation.
- Character state and mini-game reward skeletons.
- Local JSON save data.
- Safe Sync DTO generation.
- Optional privacy-safe sync push/pull client.

Server implementation is intentionally out of scope for this repository. Do not add NestJS, Prisma, PostgreSQL, or server-side source files here.

## Client/Server Boundary

The client is the only layer allowed to temporarily read raw local development data. Raw prompts, code, full logs, terminal output, Git diffs, branch names, commit messages, absolute paths, and Git remote URLs must not be stored, logged, queued for sync, or sent to a server.

The server should only receive allowlisted aggregate DTOs such as character snapshots, session summaries, work-type distributions, token buckets, and settings flags.

## Privacy-First Rules

- Store token buckets/ranges, not exact raw token values by default.
- Store project hashes and local IDs, not absolute paths.
- Treat `projectAlias` as local-only unless the user explicitly opts into syncing it.
- Never upload full `SaveData`.
- Never upload full `AgentWorkSession`.
- Build sync payloads through explicit DTO mapping.
- Run client-side forbidden field checks before persistence and sync.

## macOS Release Prep

Release metadata is validated offline before build/package/sign:

```bash
scripts/validate-release-metadata.sh
scripts/build-macos-smoke.sh
scripts/package-macos-smoke.sh
scripts/package-macos-release-candidate.sh
scripts/smoke-clean-install-macos.sh
scripts/smoke-update-migration-macos.sh
scripts/release-prep-check.sh
```

Current Unity metadata source is `UnityClient/ProjectSettings/ProjectSettings.asset`:

- Product/company: `TokenForge`
- Bundle identifier: `com.tokenforge.client`
- Version/build: `0.18.0` / `18`
- Startup scene: `Assets/_Project/Scenes/Bootstrap.unity`
- Entitlements: `BuildSupport/macOS/TokenForge.entitlements`
- Standalone app icon: `Assets/_Project/Art/AppIcon/TokenForgeReleaseIcon.png` is configured for macOS Standalone. It is original TokenForge release icon art created for this repository and documented as project-owned release art.

Developer ID signing, notarization, stapling, Gatekeeper expectations, the release candidate package command, and clean install/update migration smoke checks are documented in `Docs/macos-release.md`; normal validation does not require Apple credentials.

Local persistence uses explicit `schemaVersion` metadata. Safe save data is aggregate-only and rejects forbidden raw/private fields before write. Approved-location raw paths are local-only in `tokenforge-approved-locations.local.json` and are never mapped to Safe Sync DTOs or release packages. Backup, temp, corrupt, unsupported-schema, unsafe recovery, local token/session, contract artifact, and release package files are ignored by git.

The Phase 21 release candidate package scan rejects the retired `TokenForgePlaceholderIcon.png`, local approved-location settings, generated contract bundles, token/session files, recovery copies, `.env`, Apple/notary credentials, raw sync payload logs, local debug logs, editor-only test assemblies, source scripts that should not ship inside the app bundle, and persistence fixtures that should not ship inside the app package.

Phase 20 also adds a release-candidate UI/accessibility polish pass while preserving the prefab UI path and thin binder architecture. The Bootstrap UI now gives clearer disabled-state explanations, local-vs-remote session wording, visible privacy notices, empty-state next steps, and account/Safe Sync wording that makes explicit login, health, sync, fetch, and delete actions user-initiated only.

Phase 21 hardens the credentialed Developer ID, notarization, stapling, Gatekeeper, clean-install QA, update-migration QA, and release QA record path without making Apple credentials part of normal validation. Notarized release candidates must use `TOKENFORGE_RELEASE_SIGN_MODE=developer-id` and `TOKENFORGE_RELEASE_NOTARIZE=true`; ad-hoc builds remain for local smoke only and are expected not to pass full Gatekeeper assessment.

Phase 22 adds a local-only Safe Sync retry queue, conflict summary foundation, and tombstone/delete tracking foundation. Retry is still explicit user action only: the app does not retry on construction, login, session restore, app start, or scene load, and it does not run a background timer. Temporary sync failures can create a safe queue entry containing only operation type, timestamps, attempt counts, safe error codes, safe client session IDs, and server session ID for remote deletes. UPSERT retries regenerate the outgoing aggregate DTO from current local safe sessions through `SafeSyncMapper` and run the client privacy guard before sending; raw request/response JSON is never stored.

Phase 23 completes the explicit local saved-session delete flow. A user can select a local saved safe session and choose `Delete Local Session`; unsynced sessions are removed only from local safe save data, while synced sessions create a local tombstone so remote delete can be processed later through explicit Safe Sync tombstone/retry actions. Local delete never deletes approved locations, never sends a remote delete immediately, and never starts background sync/retry/delete work.

Phase 23 local-only sync state files are `tokenforge-sync-retry-queue.local.json`, `tokenforge-sync-conflicts.local.json`, `tokenforge-sync-tombstones.local.json`, and `tokenforge-sync-local-state.local.json`. They include `schemaVersion`, use safe corruption/future-schema recovery behavior, are gitignored, are never synced to the server, and are rejected by release package privacy scans together with retry backup/recovery and local sync-state debug files. Tombstones store only tombstone ID, safe client/server IDs, deletion time, source, status, and safe error code. Retry queue cleanup removes deleted client session IDs from pending UPSERT retries and cancels empty UPSERT entries so deleted local sessions are not re-uploaded.

Phase 24 upgrades conflict resolution and batch local sync-state management while preserving the explicit-sync privacy model. `Fetch Remote` can detect safe `RemoteDifferent`, `LocalMissing`, and expected `RemoteMissing` conflicts without applying remote data. Conflicts persist safe summaries, safe changed-field names, and, when available, a typed safe remote aggregate snapshot; raw remote JSON is never stored.

Phase 24 conflict actions remain explicit. `Keep Local` queues a safe re-upload for explicit retry using `SafeSyncMapper`; it fails safely with `LOCAL_SESSION_MISSING` if the local aggregate session is gone. `Keep Remote` validates the safe remote aggregate and applies it to local saved sessions only when the user explicitly chooses that action. If the conflict has only enough safe data to mark a decision, `Keep Remote` falls back to marker-only resolution with a safe message. Unsafe, unsupported-schema, or invalid bucket data is rejected and leaves the conflict unresolved. `Mark Resolved` records a safe resolved status without data changes, and `Cancel Resolution` leaves the conflict unresolved.

The Phase 24 conflict review UI shows a safe side-by-side local/remote summary: conflict type/status, detection time, source provider, day/time bucket, confidence, warning count, safe category/tool/language bucket summaries, shortened safe IDs, changed safe field names, and safe action explanations. It does not render raw JSON, raw paths, prompts, responses, commands, filenames, repo names, branch names, usernames, source snippets, tokens, raw logs, raw Git output, approved-location data, or raw payloads.

Phase 24 also adds explicit batch actions. Tombstones can enqueue all pending deletes, process pending deletes once, clear resolved tombstones, cancel a selected tombstone, and cancel failed tombstones. Retry entries can process all eligible entries once, cancel failed entries, clear succeeded entries, pause pending entries, and resume paused entries. Batch processing is bounded, respects retry readiness, reports safe counts, treats `NOT_FOUND`/`HTTP_404` as already applied, pauses safely on auth errors, and never touches unrelated local sessions or approved locations.

Phase 25 adds aggregate-only conflict merge policy choices, local-only conflict audit history, and optional force retry for selected retry entries. Supported merge policies are `KeepLocal`, `KeepRemote`, `PreferHigherConfidence`, `PreferNewerSafeTimestamp`, `MergeNonConflictingAggregates`, and `MarkResolvedOnly`. Policy previews are repeatable and non-mutating: they do not write save data, enqueue retries, or mark conflicts resolved. Policy application requires explicit confirmation, refuses blocked previews, revalidates typed safe remote aggregates before remote/merged writes, and records a local audit entry.

Phase 25 merge is deliberately narrow. `PreferHigherConfidence` compares only existing safe confidence buckets and blocks ties or missing/incomparable values. `PreferNewerSafeTimestamp` compares only safe day/hour buckets already present in summaries and blocks missing/equal/incomparable timestamps. `MergeNonConflictingAggregates` unions deterministic safe bucket fields when keys are absent or identical, chooses non-empty safe scalar fields only when the other side is empty, and blocks ambiguous counts or conflicting scalar/bucket values. It never merges raw text, unknown schema fields, filesystem metadata, repo/branch names, commit hashes, prompts, responses, commands, source snippets, or approved-location data.

Phase 25 conflict audit history is stored only in `tokenforge-sync-conflict-audit.local.json`. The file uses schema v1, corruption/future-schema safe loading, privacy validation before save/load, gitignore coverage, release package rejection, and retention trimming to the latest 200 entries. Audit entries contain safe conflict IDs, policy/action/status, safe diff field names, safe before/after summaries, queued retry ID, safe warning IDs, and safe message code only.

Phase 26 adds release hardening for Developer ID notarization readiness and Safe Sync confirmation-modal UX. `scripts/notarize-macos-release-candidate.sh` defaults to credential-free readiness mode and never claims notarization without a successful submit, staple, and verification pass.

Phase 26 Safe Sync UI now gates merge apply, Keep Local, Keep Remote, Mark Resolved, force retry, retry batch updates, tombstone batch updates, and clearing resolved audit history behind a dedicated confirmation request. High-risk actions require typed confirmation such as `MERGE`, `FORCE RETRY`, `CANCEL DELETE`, or `CLEAR HISTORY`. Confirmation text is generated from safe aggregate summaries only and must not render raw paths, raw JSON, prompts, responses, commands, source snippets, tokens, logs, repo names, branch names, commit hashes, stack traces, or approved-location paths.

Phase 27 adds the production release-machine path while keeping local validation credential-free. `scripts/sign-macos-release-candidate.sh` dry-runs by default and reports `SIGNING_READY_BUT_IDENTITY_MISSING` when no Developer ID identity is available; `--sign` requires `DEVELOPER_ID_APPLICATION`, signs with hardened runtime and timestamp, verifies, and re-packages the release candidate. `scripts/notarize-macos-release-candidate.sh --submit` requires a Developer ID signed app plus `APPLE_ID`, `APPLE_TEAM_ID`, and `APPLE_APP_SPECIFIC_PASSWORD`; ad-hoc submit is blocked as `NOTARIZATION_BLOCKED_ADHOC_SIGNED`. `scripts/generate-release-readiness-report.sh` now writes report schema v2, and `scripts/collect-release-evidence.sh` writes `/tmp/tokenforge-release/evidence/` with safe JSON/Markdown/checksum evidence. `scripts/gatekeeper-qa-helper.sh` inspects Gatekeeper QA status without disabling Gatekeeper or removing quarantine.

Required release-machine environment variables are documented without values: `DEVELOPER_ID_APPLICATION`, `APPLE_ID`, `APPLE_TEAM_ID`, and `APPLE_APP_SPECIFIC_PASSWORD`. Do not write credentials to repo files or generated evidence.

Phase 28 adds the one-command release operator path. `scripts/run-credentialed-release.sh` defaults to a safe dry-run and writes `/tmp/tokenforge-release/credentialed-release-summary.json`; `scripts/run-credentialed-release.sh --credentialed` fails fast unless Developer ID and Apple notarization credentials are present. The credentialed path signs, submits, staples, validates stapling, validates `spctl`, generates readiness report schema v4, collects final evidence, and writes `/tmp/tokenforge-release/evidence/final-release-handoff.md`.

Phase 29 adds the post-credentialed verification loop and final release record. `scripts/verify-credentialed-release.sh` verifies an already-built release candidate and writes `/tmp/tokenforge-release/credentialed-release-verification.json`. `scripts/update-gatekeeper-qa-status.sh` creates and updates the manual Gatekeeper QA checklist. `scripts/generate-final-release-record.sh` writes `/tmp/tokenforge-release/evidence/final-release-record.md` and `.json` for attachment to a GitHub Release or internal release record.

Phase 30 freezes the MVP release-candidate metadata and adds the release-operator handoff path. `Docs/release-freeze-manifest-v0.18.0-build18.md` records the frozen dry-run/ad-hoc candidate, checksum, expected credentialed command, required environment variable names, limitations, and privacy assertions. `scripts/generate-release-operator-handoff.sh` writes `/tmp/tokenforge-release/evidence/release-operator-handoff.md` and `.json`; `scripts/prepare-release-tag.sh` suggests `v0.18.0-build18` without creating or pushing it by default; `scripts/final-privacy-regression-audit.sh` writes `/tmp/tokenforge-release/evidence/final-privacy-regression-audit.json`.

Phase 31 finalizes the credentialed release-machine sequence and tag guardrails. `scripts/run-release-machine-finalization.sh` defaults to a credential-free checklist summary at `/tmp/tokenforge-release/release-machine-finalization-summary.json`; `--credentialed` requires `DEVELOPER_ID_APPLICATION`, `APPLE_ID`, `APPLE_TEAM_ID`, and `APPLE_APP_SPECIFIC_PASSWORD`, then runs release prep, credentialed signing/notarization, credentialed verification, Gatekeeper QA helper/status summary, final release record, release operator handoff, final privacy audit, and release prep again. It reports `RELEASE_MACHINE_PENDING_QA`, `RELEASE_MACHINE_READY_FOR_TAG`, or `RELEASE_MACHINE_BLOCKED`.

Readiness report schema v5 reports `releaseStatus` as `dryRunReady`, `credentialedSigned`, `notarized`, `stapled`, `spctlVerified`, `pendingQa`, `evidencePending`, `readyForDistribution`, or `blocked`. `readyForDistribution` is only valid when Developer ID signing, hardened runtime, notarization, stapling, `spctl`, package privacy scan, final privacy audit, Gatekeeper QA, credentialed verification, and evidence lock all pass. Ad-hoc builds can only claim local package/readiness smoke; they cannot claim notarization, stapling, `spctl` distribution readiness, final evidence lock readiness, or final Gatekeeper distribution readiness.

Phase 31 adds `scripts/validate-release-freeze-manifest.sh` for SHA/freeze-manifest drift handling and `scripts/lock-release-evidence.sh` for the final attachable evidence archive. Dry-run rebuilds may legitimately produce a different zip SHA; that state is allowed only when the manifest documents dry-run SHA drift and no distribution-ready claim is made. A credentialed final `readyForDistribution` state requires the final SHA in final release evidence. Evidence lock writes `/tmp/tokenforge-release/evidence/evidence-lock-summary.json`; final ready state creates `/tmp/tokenforge-release/TokenForge-0.18.0-build18-release-evidence.zip` and records its checksum. If QA is pending, the lock stays `EVIDENCE_LOCK_PENDING_QA`; ad-hoc state stays `EVIDENCE_LOCK_DRY_RUN_ONLY`.

Phase 32 adds the final release-publication tooling layer. `scripts/verify-final-distribution-readiness.sh` writes `/tmp/tokenforge-release/final-distribution-readiness.json` and reports `FINAL_DISTRIBUTION_READY`, `FINAL_DISTRIBUTION_DRY_RUN_ONLY`, `FINAL_DISTRIBUTION_PENDING_QA`, `FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK`, or `FINAL_DISTRIBUTION_BLOCKED`. It requires matching release-candidate SHA-256 values across readiness, final release record, evidence lock, and the current zip before final readiness can be claimed. `scripts/generate-github-release-draft.sh` writes safe GitHub Release draft Markdown/JSON, `scripts/generate-release-bundle-index.sh` indexes final artifacts/evidence, and `scripts/print-release-status-dashboard.sh` prints a concise operator dashboard. `scripts/prepare-release-tag.sh --create --yes` now requires `FINAL_DISTRIBUTION_READY`; `--push --yes` requires the local tag plus the same final gates.

Phase 33 closes the final publication loop without adding product behavior. `scripts/validate-github-release-assets.sh` writes `/tmp/tokenforge-release/github-release-assets-validation.json` and reports `GITHUB_RELEASE_ASSETS_READY`, `GITHUB_RELEASE_ASSETS_DRY_RUN_ONLY`, `GITHUB_RELEASE_ASSETS_PENDING_DISTRIBUTION_READY`, or `GITHUB_RELEASE_ASSETS_BLOCKED`. It validates SHA-256 consistency across final readiness, readiness report, final release record, GitHub Release draft, and bundle index; checks the release zip and safe evidence archive; and rejects unsafe local-only evidence. `scripts/generate-github-release-publication-plan.sh` writes safe non-publishing Markdown/JSON with suggested `gh` commands. `scripts/publish-github-release.sh` is dry-run only by default and publishes only with `--publish --yes` after final distribution, asset validation, evidence lock, privacy audit, local tag, release prep, and GitHub authentication gates pass. `scripts/generate-post-release-audit.sh` records whether publication actually happened. The dashboard is now v2 and includes GitHub asset validation, publication plan, publication summary, post-release audit, local tag existence, and the next release-loop action.

Phase 29 release sequence:

```bash
scripts/run-credentialed-release.sh
scripts/release-prep-check.sh --summary-only

# On the release machine only, after setting required credential environment variables in the shell:
scripts/run-credentialed-release.sh --credentialed

scripts/update-gatekeeper-qa-status.sh init
scripts/update-gatekeeper-qa-status.sh set cleanInstall passed
scripts/update-gatekeeper-qa-status.sh note cleanInstall "Clean install completed with expected Gatekeeper behavior."
scripts/update-gatekeeper-qa-status.sh summary
scripts/update-gatekeeper-qa-status.sh validate

scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/generate-release-readiness-report.sh
scripts/generate-release-operator-handoff.sh
scripts/prepare-release-tag.sh
scripts/final-privacy-regression-audit.sh
scripts/validate-release-freeze-manifest.sh
scripts/run-release-machine-finalization.sh
scripts/lock-release-evidence.sh
```

Dry-run or ad-hoc output remains `dryRunReady` / `CRED_RELEASE_VERIFY_DRY_RUN_ONLY`. If signing, notarization, stapling, `spctl`, and privacy pass but QA is missing or in progress, readiness is `pendingQa` and verification is `CRED_RELEASE_VERIFIED_PENDING_QA`.

Phase 31 release-machine command sequence:

```bash
scripts/run-release-machine-finalization.sh

# On the release machine only, after setting required credential environment variables in the shell:
scripts/run-release-machine-finalization.sh --credentialed
```

Phase 31 post-QA command sequence:

```bash
scripts/update-gatekeeper-qa-status.sh summary
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
scripts/generate-release-operator-handoff.sh
scripts/final-privacy-regression-audit.sh
scripts/lock-release-evidence.sh
scripts/generate-release-readiness-report.sh
scripts/prepare-release-tag.sh
```

Exact tag creation sequence after every gate passes:

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
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/print-release-status-dashboard.sh
scripts/prepare-release-tag.sh --create --yes

# Optional and explicit only after local tag creation and all gates still pass:
scripts/prepare-release-tag.sh --push --yes
```

Exact Phase 33 command sequence after `FINAL_DISTRIBUTION_READY`:

```bash
scripts/verify-final-distribution-readiness.sh
scripts/generate-github-release-draft.sh
scripts/generate-release-bundle-index.sh
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/prepare-release-tag.sh --create --yes

# Optional after local tag creation and all gates still pass:
scripts/prepare-release-tag.sh --push --yes

scripts/publish-github-release.sh

# Optional actual publish only after reviewing the dry-run:
scripts/publish-github-release.sh --publish --yes

scripts/generate-post-release-audit.sh
scripts/print-release-status-dashboard.sh
scripts/release-prep-check.sh
```

`scripts/prepare-release-tag.sh` never creates or pushes in default mode. `--create --yes` and `--push --yes` are blocked with `TAG_BLOCKED_RELEASE_NOT_READY` unless final distribution verification is `FINAL_DISTRIBUTION_READY`, release-machine finalization is `RELEASE_MACHINE_READY_FOR_TAG`, readiness is `readyForDistribution`, verification is `CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION`, QA is `passed`, final privacy audit is `FINAL_PRIVACY_AUDIT_PASSED`, freeze validation is a final match, and evidence lock is `EVIDENCE_LOCK_READY`.

Dry-run/ad-hoc Phase 32 sequence:

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
scripts/validate-github-release-assets.sh
scripts/generate-github-release-publication-plan.sh
scripts/publish-github-release.sh
scripts/generate-post-release-audit.sh
scripts/print-release-status-dashboard.sh
scripts/prepare-release-tag.sh
```

GitHub Release attachments can include the final release candidate zip, final release notes, and, only when `EVIDENCE_LOCK_READY` and asset validation passes, `TokenForge-0.18.0-build18-release-evidence.zip`. Do not upload local-only state files, approved-location data, raw logs, credential files, `.env` files, raw server bodies, prompts, responses, source snippets, stack traces, repo names, branch names, commit hashes, or tokens. Before notarization and final distribution readiness, the release draft, publication plan, publish summary, and post-release audit must say the release is not public distribution-ready and not published.

Phase 33 troubleshooting:

- Final distribution not ready: run credentialed finalization, complete QA, lock evidence, regenerate readiness, then rerun final distribution verification.
- Asset mismatch: rebuild or regenerate summaries from the current release candidate until every recorded SHA-256 matches the zip.
- Tag missing: run `scripts/prepare-release-tag.sh --create --yes` only after final gates pass.
- GitHub Release already exists: `scripts/publish-github-release.sh --publish --yes` stops without overwrite; use `--update-existing` only to update notes and upload missing safe assets.
- `gh` CLI missing or auth unavailable: dry-run remains valid; guarded publish blocks without printing or storing tokens.
- Unsafe evidence detected: remove the unsafe file/content, regenerate evidence summaries, rerun privacy audit, lock evidence, and validate assets again.
- Publication succeeded but audit missing: run `scripts/generate-post-release-audit.sh` and `scripts/print-release-status-dashboard.sh`.

MVP release handoff checklist:

- `scripts/release-prep-check.sh --summary-only` passes.
- `scripts/final-privacy-regression-audit.sh` passes.
- `scripts/generate-release-operator-handoff.sh` generates the attachable handoff.
- `scripts/run-credentialed-release.sh --credentialed` is run on the release machine.
- Gatekeeper QA is completed with `scripts/update-gatekeeper-qa-status.sh`.
- `scripts/generate-final-release-record.sh` is regenerated after credentialed verification.
- `scripts/prepare-release-tag.sh` suggests `v0.18.0-build18`; tag creation remains explicit.
- Phase 33 asset validation, publication plan, publish dry-run, post-release audit, and dashboard v2 are generated before any actual GitHub Release publication.
- Release evidence from `/tmp/tokenforge-release/evidence/` is attached only after review and only as the safe evidence archive when ready.

Phase 25 force retry is an explicit selected-entry action. It requires confirmation, may bypass `nextAttemptAt`, still blocks auth-paused entries until the user resumes authentication, and still blocks unsafe/invalid payloads and permanent failures. It does not add background processing and does not process retry entries automatically.

Phase 25 validation commands:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD/UnityClient" -runTests -testPlatform editmode -testResults /tmp/tokenforge-editmode-results.xml -logFile /tmp/tokenforge-unity-editmode.log
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD/UnityClient" -runTests -testPlatform playmode -testResults /tmp/tokenforge-playmode-results.xml -logFile /tmp/tokenforge-unity-playmode.log
scripts/validate-release-metadata.sh
scripts/export-safe-sync-contract-bundle.sh
scripts/package-macos-smoke.sh
scripts/package-macos-release-candidate.sh
```

Known Phase 25 limits: conflict merge remains aggregate-only, raw data recovery is impossible by design, no automatic background sync/retry/tombstone/conflict processing exists, Safe Sync schemaVersion remains 1, persistence schema remains v1, no server-side merge is claimed, live tests are opt-in only, and Developer ID/notarization still require release-machine credentials.

Credentialed release-machine command. Set the required credential environment variables in the release shell; values are intentionally omitted from documentation:

```bash
scripts/run-credentialed-release.sh --credentialed
scripts/verify-credentialed-release.sh
scripts/generate-final-release-record.sh
```

Explicit release verification commands:

```bash
scripts/verify-macos-signing.sh
scripts/notarize-macos-app.sh
scripts/staple-macos-app.sh
spctl --assess --type execute --verbose /tmp/tokenforge-macos-build/TokenForge.app
```

Local Phase 21 validation on 2026-05-15 passed release metadata, EditMode, PlayMode, contract export/server validation, macOS build smoke, package smoke, release prep, release candidate packaging, clean install structural smoke, update migration smoke, and ad-hoc signing smoke. It regenerated `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip` as an ad-hoc release candidate with notarization disabled and privacy scan passed. Developer ID signing/notarization was safely blocked by missing release-machine credentials; clean-install GUI QA and packaged update-migration GUI QA remain documented as not run on this non-credentialed local pass.

## Phase 3 Real Backend Smoke Sync

The Phase 3 smoke path can call the completed TokenForgeCoreServer Phase 2 privacy-safe backend during local development. It is optional and must be explicitly enabled through `BootstrapSyncMode.RealBackendSmoke`.

Backend dev URL:

```text
http://localhost:3000/api/v1
```

Endpoints used by the smoke flow:

```text
POST /api/v1/auth/guest
POST /api/v1/sync/push
GET  /api/v1/sync/pull
```

For local development, set the `apiBaseUrl` field on `AppBootstrapper` to `http://localhost:3000/api/v1`, or construct `ApiConfiguration.CreateLocalDevelopment()` in tests/tools. `ApiConfiguration` remains the source of truth for the backend base URL so production can be switched later without changing `SyncService`.

The real-backend smoke path uses `DevGuestAuthTokenProvider`. It calls `/auth/guest`, caches the bearer token only in memory for the current app session, injects it through `IAuthTokenProvider`, and never writes it to SaveData. Tests use fake transports and do not require a real backend.

Manual local smoke run:

1. Start TokenForgeCoreServer on `localhost:3000`.
2. Open `UnityClient/` in Unity.
3. Set `AppBootstrapper.bootstrapSyncMode` to `RealBackendSmoke`.
4. Confirm `AppBootstrapper.apiBaseUrl` is `http://localhost:3000/api/v1`.
5. Run the client.
6. Confirm safe logs show guest auth success/failure, push accepted counts, pull returned counts, and merge completion without tokens or raw bodies.

## Phase 4 Git Aggregate Analyzer

Phase 4 adds a privacy-safe Git aggregate analyzer for a user-selected local repository. The client may temporarily inspect the selected folder in memory, but it immediately reduces Git output into aggregate buckets before creating a `GIT` session summary.

Git commands used during local analysis:

```text
git rev-parse --is-inside-work-tree
git status --porcelain
git diff --numstat
git diff --cached --numstat
git log --since=<window>.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n <maxCommits>
```

What it analyzes:

- Whether the selected folder is a Git working tree.
- Uncommitted changed-file status counts.
- Added/deleted line buckets from `--numstat`.
- Recent commit count bucket within the selected window.
- Safe file kind buckets such as modified, created, deleted, renamed, and binary.
- Safe extension/category buckets such as `csharp`, `typescript`, `javascript`, `json`, `markdown`, `config`, `test`, `asset`, and `other`.

What it persists:

- Hashed repository identifier only.
- Day-level analysis time bucket.
- Source provider `GIT`.
- Aggregate changed-file count plus file/commit buckets.
- Added/deleted line buckets.
- Extension/category bucket counts.
- Confidence level.
- Generic session alias: `Git aggregate session`.
- Growth deltas derived from aggregate buckets.
- Safe warning category IDs.
- Analyzer version.

What it never stores, syncs, or logs:

- Absolute or relative paths, file names, repo names, branch names, remotes, commit hashes, commit messages, diffs, source code, terminal output, author names/emails, tokens, secrets, authorization strings, or arbitrary Git text payloads.

Manual local development flow:

1. Open `UnityClient/` in Unity.
2. Construct a `GitRepositoryAnalysisInput` with a user-selected local repository path.
3. Pass it to `GitAnalysisSessionProvider.AnalyzeAndSaveAsync`.
4. Confirm the generated session has `SourceProvider == "GIT"` and `GitChangeSummary.AnalyzerVersion == "git-aggregate-v1"`.
5. Sync normally through `SyncService` if cloud sync is enabled; the existing safe mapper sends only aggregate DTO fields.

Privacy guarantees:

- `RepositoryRootPath` exists only in the in-memory input and command runner boundary.
- The analyzer never reads remotes or branch names.
- Commit messages are ignored by using a fixed commit delimiter in `git log`.
- Raw command output is parsed inside the analyzer and discarded.
- Save and sync paths still run `PrivacySanitizer` and forbidden-field checks before persistence or transport.

Known limitations:

- Claude/Codex parsing is limited to the Phase 6 sanitized parser foundation.
- No raw diff inspection.
- No per-file history persistence.
- No branch or remote tracking.
- No delete/tombstone sync.
- Git analysis is aggregate-only.
- UI may still be minimal or dev-only.

## Phase 5 Git Repository Picker and Review Flow

Phase 5 adds the user-facing Unity/macOS flow around the Phase 4 analyzer. The client now lets a user select a local Git repository, run aggregate analysis, review the safe summary, confirm saving the generated session, and optionally trigger the existing sync service when it is configured.

User flow:

1. Open the Unity client scene.
2. Use `Select Git Repository` to choose a local folder. In Unity Editor/development builds, a manual dev path field is also available before analysis.
3. Adjust analysis settings: analysis window days, include uncommitted changes, include recent commits, and max commits to inspect.
4. Run `Analyze`.
5. Review only the safe aggregate summary.
6. Press `Save Session` to persist the reviewed session and derived growth.
7. Press `Sync Now` only when sync is configured and the session has already been saved.

What the review can show:

- Source provider `GIT`.
- Generic session alias.
- Analysis window days.
- Changed-file, added-line, deleted-line, and commit-count buckets.
- Extension/category bucket summary.
- Confidence level.
- Privacy warning count and safe warning category IDs.
- Derived EXP/stat deltas.
- Analyzer version.

What is never shown, stored, synced, or logged after selection:

- Raw repository path, repository name, branch name, remote URL, commit hash, commit message, file name, file path, diff, command output, source code, author data, bearer token, authorization header, API key, secret, or raw request/response body.

Persistence behavior:

- The selected repository path is held only in memory long enough to build and run `GitRepositoryAnalysisInput`.
- Analysis produces a pending safe review model in memory.
- Nothing is saved until the user confirms with `Save Session`.
- Save still runs `PrivacySanitizer` / forbidden-field validation before writing.
- The saved session contains only the safe Git aggregate summary and derived character/progression data.

Optional sync behavior:

- `Sync Now` uses the existing `SyncService`; Phase 5 does not add another sync implementation.
- Sync is disabled in the UI unless the runtime bootstrap has configured sync.
- Sync is only available after `Save Session`.
- Sync status uses safe success/failure categories and does not expose tokens, payload bodies, or raw identifiers.

Development flow:

1. Open `UnityClient/` in Unity.
2. Run the main scene.
3. In the Git Repository Analysis panel, use the Editor folder picker or the development-only manual path field.
4. Analyze and review the aggregate summary.
5. Save locally; enable a bootstrap sync mode only when testing optional sync.

Known limitations:

- UI is still MVP/dev-polish level.
- Claude/Codex parsing is limited to the Phase 6 sanitized parser foundation.
- No raw diff inspection.
- No per-file history persistence.
- No branch/remote tracking.
- No delete/tombstone sync.
- Production login is still not implemented.
- Repository path is only used in memory during analysis.

## Phase 6 AI Agent Activity Parser Foundation

Phase 6 adds a privacy-safe Claude/Codex agent activity parser foundation and provider polling orchestration. This is a research/MVP domain layer, not a private conversation reader.

What AI Agent analysis does:

- Reads only a user-approved local agent log location for the current explicit analysis request.
- Parses metadata-like signals such as timestamps, broad session/event counts, broad tool categories, and safe language/category buckets.
- Produces an `AI_AGENT` work session with an `AgentActivitySummary`.
- Stores and syncs only hashes, day buckets, count buckets, enum categories, confidence, warning IDs, and parser version.
- Requires review before saving, and sync remains available only after a safe save confirmation.

What it explicitly does not collect:

- Raw prompts, response text, chat contents, AI output, source text, diffs, command strings, terminal output, stdout/stderr, file paths, file names, repo names, branch names, remotes, commit hashes, author data, usernames, tokens, secrets, or authorization strings.
- Semantic interpretation of private conversations.
- Background scans of arbitrary folders.

Privacy boundary:

- `AgentAnalysisInput.SelectedLocationPath` is memory-only and is never copied into save data, review models, logs, or sync DTOs.
- `FileAgentLogSourceReader` reads candidate log files only from the selected path and hands raw lines to the parser in memory.
- Parser classes discard raw strings after deriving aggregate buckets.
- `PrivacySanitizer`, `ForbiddenFieldDetector`, save validation, and sync payload validation reject forbidden field names and risky string patterns before persistence or transport.

Supported provider status:

- Claude: sanitized parser foundation only.
- Codex: sanitized parser foundation only.
- Unknown: conservative fallback that returns low confidence and safe warning IDs for unsupported formats.

Provider orchestration:

- `IActivityProvider`, `ActivityProviderPoller`, `ActivityAnalysisCoordinator`, and `ActivityProviderResult` coordinate Git and AI Agent providers.
- Polling requires explicit approval on both the poll request and each selected provider location.
- Provider failures are reduced to safe categories.
- The existing Git repository analysis path remains parallel and unchanged.

Known limitations:

- Parser coverage is intentionally conservative and sample-driven.
- No production login.
- No cloud tombstone sync.
- No raw log persistence.
- No branch/remote tracking.
- No per-file history persistence.
- UI integration is minimal; the main work is testable domain/application code.

## Sync Data Contract

Allowed to sync:

- Character snapshot fields: character ID, display name, level, total EXP, evolution type, bounded stats, unlocked item IDs.
- Safe session summaries: session ID, agent/work/result enums, timestamps, token usage bucket, line-change buckets, changed-file count, aggregate action counts, confidence, safe project hash, source provider IDs.
- Achievement progress: safe achievement IDs, unlocked timestamp, bounded progress.
- Work-type distribution counts.
- Privacy/settings flags such as cloud sync, telemetry opt-in, and project alias masking.
- Sync metadata such as sync version and last sync timestamp.

Supported backend `sourceProvider` transport enum values:

```text
UNITY_CLIENT
AI_AGENT
CODEX
CLAUDE
GIT
MANUAL
UNKNOWN
```

Must never be synced, stored, queued, or logged:

- Raw prompts, raw code, source files, raw logs, terminal output, stdout/stderr.
- Git diffs, patches, exact line contents, branch names, commit messages.
- Absolute file paths, repo URLs, Git remotes, raw project names, API keys, secrets, bearer tokens.
- Claude/Codex raw log contents or real project content.

Current MVP limitations:

- Sync is local-first and optional; failed network calls return structured safe errors and keep local save data intact.
- Bootstrap sync is opt-in through `BootstrapSyncMode`; app startup does not require a server.
- Development guest auth is only for smoke testing and is not final production login.
- No tombstone/delete sync yet.
- Claude/Codex log parsing is a sanitized Phase 6 foundation only.
- Git execution is limited to the Phase 4 aggregate analyzer and is not part of the sync smoke flow.
- Pull merge is additive/upsert-based and does not delete local sessions unless a future API adds explicit deletion/tombstones.
- Sync remains privacy-safe aggregate-only.
- Real backend smoke flow is optional and should not run unintentionally in tests.

## Phase 7 Approved Analysis UI

The Unity bootstrap screen now exposes a minimal approved-location review flow for local Git and AI agent log analysis:

- `Git Analysis`: select a repository, then explicitly run Git aggregate analysis.
- `AI Agent Log Analysis`: select an approved log folder or file, choose `Claude`, `Codex`, or `Unknown/Auto`, then explicitly run agent analysis.
- `Review`: shows only safe aggregate review data and requires `Save Session` before anything is persisted.
- `Recent Safe Sessions`: lists saved summaries from `SaveDataRepository` without source locations.
- `Privacy Notice`: states that analysis is user-approved and aggregate-only.

Phase 7 privacy guarantees:

- Raw repository paths and selected agent log paths are memory-only during selection and analysis, then cleared by analyze or discard paths.
- Raw prompts, responses, commands, filenames, repository names, branch names, usernames, source-code-like snippets, secrets, tokens, and raw logs are not saved or synced.
- Saved sessions continue to use safe aggregate domain models only.
- Sync DTOs remain aggregate-only through `SafeSyncMapper`; no approved-location settings are mapped to sync payloads.
- No background scanning or automatic analysis is introduced in this phase.

## Phase 8 Local-Only Approved Locations

Phase 8 adds optional approved-location settings that are stored only on the local device in a separate file:

```text
tokenforge-approved-locations.local.json
```

These settings are managed by `ApprovedLocationSettingsRepository`, separate from `SaveDataRepository` and separate from sync DTOs. Entries may contain:

- Local-only ID.
- User-approved display alias.
- Source type: `Git`, `Claude`, `Codex`, or `UnknownAuto`.
- Raw local path, kept only in the local-only approved-location settings file.
- Created/updated timestamps.
- Enabled flag.

The bootstrap UI can now:

- Add the currently selected Git repository as a local-only approved location.
- Add the currently selected agent log folder/file as a local-only approved location.
- Select, disable, or remove approved Git and agent locations.
- Use approved locations only as a selection shortcut; analysis still requires an explicit `Analyze Git Activity` or `Analyze Agent Activity` action.

Local-only privacy guarantees:

- Approved locations are stored only on this device and are never synced.
- Approved-location raw paths are not written into `SaveData`.
- Approved-location raw paths are not included in `SafeSyncDtos`.
- `SafeSyncMapper` does not read or emit approved-location settings.
- Recent safe session summaries do not show approved-location raw paths.
- Removing or disabling an approved location does not delete already saved safe aggregate sessions.

What is saved as safe session data remains aggregate-only: provider/source IDs, day buckets, count buckets, line-change buckets, broad tool/language categories, confidence, warning IDs, parser/analyzer version, growth results, and other bounded gameplay/session metadata.

What is synced remains aggregate-only: character snapshot, safe session summaries, work-type distribution, safe settings flags, achievements, and sync metadata.

What is never saved or synced: raw prompts, raw responses, raw commands, filenames, repository names, branch names, usernames, source-code-like snippets, secrets, tokens, raw logs, raw Git output, and approved-location raw paths.

Phase 8 also broadens sanitized Claude/Codex parser fixtures for Claude-like JSONL logs, Codex-like session JSON logs, mixed/unknown logs, malformed entries, partial schemas, timestamped tool activity, code-like content, path-like strings, command-like strings, and token-like strings. Parser outputs remain safe aggregate-only and risky content produces safe warning IDs and lowered confidence instead of leaked content.

## Phase 9 Safe Sync Contract Bundle

Phase 9 adds a Unity-generated Safe Sync client contract bundle for server-side drift detection. The bundle is generated locally by the Unity client and can be validated by TokenForgeCoreServer without requiring the server repository to live inside this repository.

Default generated output:

```text
UnityClient/artifacts/client-contract/unity-safe-sync-contract-v1.bundle.json
```

Local helper script:

```bash
scripts/export-safe-sync-contract-bundle.sh
```

Override paths when needed:

```bash
UNITY_PATH=/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
OUTPUT_PATH=/tmp/unity-safe-sync-contract-v1.bundle.json \
scripts/export-safe-sync-contract-bundle.sh
```

Direct Unity batch-mode export:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput /tmp/unity-safe-sync-contract-v1.bundle.json \
  -quit \
  -logFile /tmp/tokenforge-contract-export.log
```

Manual server validation from TokenForgeCoreServer:

```bash
npm run client-contract:validate -- --bundle /path/to/TokenForge/UnityClient/artifacts/client-contract/unity-safe-sync-contract-v1.bundle.json
```

What the valid fixture includes:

- A `schemaVersion: 1` `/api/v1/sync/activity-sessions` request body.
- Deterministic safe sample sessions for `MANUAL`, `GIT`, `CLAUDE`, `CODEX`, and `UNKNOWN_AGENT`.
- Aggregate-only fields derived through `SafeSyncMapper`: client session IDs, source provider enums, day/time buckets, confidence, safe warning IDs, parser/analyzer versions, safe category/language/tool buckets, hashed repository IDs, and count/line/session/interaction buckets.

What is intentionally included only in the unsafe fixture:

- Forbidden field names and raw-looking values such as approved-location paths, repo/branch/file names, commands, prompts, responses, raw logs, API-key-like strings, and source-text-like content.
- This fixture exists only so the server validator can confirm rejection behavior; it is not used by production sync.

Privacy guarantees:

- The valid fixture is generated from deterministic safe domain sessions through the actual `SafeSyncMapper` export path.
- Approved locations remain local-only in `tokenforge-approved-locations.local.json` and are never exported as valid sync data.
- Raw local paths, prompts, responses, commands, filenames, repo names, branch names, usernames, source snippets, secrets, tokens, raw logs, raw Git output, and approved-location settings are not present in the valid fixture, production sync DTOs, saved sessions, logs, or review models.
- The export command logs only safe metadata: output path, bundle version, schema version, valid session count, and provider enum list. It does not print fixture payloads.

Current limitations:

- Schema version 1 only.
- The generated bundle is for contract validation only.
- Generated bundle artifacts are ignored by git unless intentionally promoted elsewhere.

## Phase 10 Safe Sync API Integration

Phase 10 integrates the real TokenForgeCoreServer Safe Sync API for explicit user-triggered activity session sync. The Unity client still owns local analysis and privacy reduction; the server receives only aggregate contract DTOs generated by `SafeSyncMapper.ToActivitySessionsContractRequest(...)`.

Server endpoints used:

```text
GET    /api/v1/sync/health
POST   /api/v1/sync/activity-sessions
GET    /api/v1/sync/activity-sessions
DELETE /api/v1/sync/activity-sessions/:id
```

Client integration:

- `SafeSyncApiConfig` holds the configurable base URL. The default is local development (`http://localhost:3000/api/v1`), not a production URL.
- `IAuthTokenProvider` injects bearer tokens. `StaticAuthTokenProvider` is for tests/dev wiring, `NullAuthTokenProvider` keeps local unauthenticated mode explicit, and no full login UI is implemented yet.
- `SafeSyncApiClient` supports health, post, fetch, and delete with timeouts through the existing transport layer.
- `SafeSyncService.SyncNowAsync()`, `FetchRemoteSessionsAsync()`, and `DeleteRemoteSessionAsync(...)` are explicit action methods. There are no timers, polling loops, background sync jobs, or auto-sync on construction.
- The bootstrap UI adds Safe Sync controls for base URL, health check, syncing safe sessions, fetching remote safe sessions, and deleting a selected remote safe session.

What is synced:

- `schemaVersion: 1` activity session contract requests.
- Client session IDs, source provider enums (`MANUAL`, `GIT`, `CLAUDE`, `CODEX`, `UNKNOWN_AGENT`), day/time buckets, confidence, safe warning IDs, analyzer/parser versions, hashed repository IDs, aggregate count/line/session/interaction/duration buckets, and safe category/language/tool buckets.

What is never synced, logged, or stored as sync data:

- Approved locations or `tokenforge-approved-locations.local.json` contents.
- Raw local paths, prompts, responses, commands, filenames, repository names, branch names, usernames, source snippets, secrets, tokens, raw logs, raw Git output, request bodies, or response bodies.

Privacy guard:

- Production sync sends only `SafeSyncMapper` contract output.
- Before any network send, `SafeSyncService` validates the outgoing payload with the sync DTO allowlist and forbidden-field detector.
- If a forbidden field or risky value is detected, the request is blocked locally with `CLIENT_PRIVACY_GUARD_BLOCKED_PAYLOAD`.
- Logs contain only safe metadata: endpoint name, status code, accepted/rejected counts, safe error code, duration, and schema version.
- Server unavailable/auth failures do not delete local saved sessions.

Contract bundle export:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput /tmp/unity-safe-sync-contract-v1.bundle.json \
  -quit \
  -logFile /tmp/tokenforge-contract-export.log
```

Server validation:

```bash
cd /path/to/server
npm run client-contract:validate -- --bundle /tmp/unity-safe-sync-contract-v1.bundle.json
```

EditMode tests:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform editmode \
  -testResults /tmp/tokenforge-editmode-results.xml \
  -logFile /tmp/tokenforge-unity-test.log
```

Known limitations:

- Schema version 1 only.
- No background sync.
- No conflict resolution.
- No tombstone sync.
- No automatic retry queue yet.

## Phase 11 Safe Sync Authentication

Phase 11 adds production-oriented authentication integration for Safe Sync while keeping local analysis and approved-location management fully usable when logged out.

Server auth contract confirmed from the current TokenForgeCoreServer code:

```text
POST /api/v1/auth/signup
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
GET  /api/v1/users/me
```

Login and signup accept `email` and `password`; signup also accepts optional `nickname`. Login/signup return `user`, `accessToken`, and `refreshToken`. Refresh accepts `refreshToken` and returns a rotated `accessToken` and `refreshToken`. Logout accepts `refreshToken` and returns `status: "ok"`. Auth errors use safe `errorCode` values such as `VALIDATION_FAILED` or `AUTH_REQUIRED`; the client never logs response bodies.

Client auth classes:

- `AuthApiClient`, `IAuthApiClient`, `AuthApiConfig`, `AuthApiModels`, and `AuthApiError` implement the server contract with configurable base URL and timeout handling.
- `ISecureTokenStore`, `MacOSKeychainTokenStore`, and `InMemoryTokenStore` isolate token persistence.
- `AuthSessionService`, `IAuthSessionService`, `AuthSession`, `AuthState`, and `AuthResult` own login, startup session loading, refresh, logout, and token metadata.
- `AuthSessionTokenProvider` adapts the session service to the existing `IAuthTokenProvider` used by `SafeSyncApiClient`.

Secure token storage strategy:

- Production macOS runtime uses the macOS Keychain through `MacOSKeychainTokenStore`.
- Tests use `InMemoryTokenStore`.
- Tokens are not stored in `PlayerPrefs`, save data, JSON files, approved-location settings, or logs.
- UI state exposes only auth state, current user summary, safe error code, and token expiration time. It never exposes access-token or refresh-token values.

Login/logout flow:

- The bootstrap UI has a Safe Sync Login section with server base URL, email, password, login, logout, auth status, current user summary, and expiration status.
- Login only establishes the auth session. It does not trigger sync.
- Logout revokes the refresh token when available, clears local token storage, and leaves local saved sessions intact.
- Logged-out users can still analyze Git, analyze agent logs, save local sessions, review safe summaries, and manage local-only approved locations.

Safe Sync auth behavior:

- Health checks remain unauthenticated.
- Activity-session post, fetch, and delete require a bearer token from `AuthSessionTokenProvider`.
- Missing token returns `AUTH_REQUIRED` locally without sending the request.
- Expired access tokens refresh before sync when a refresh token is available.
- If the server rejects a sync call with `401`/`403`, the client attempts one refresh and retries the original request once. It does not retry infinitely.
- Refresh failure clears the stored session, marks auth expired/auth-required, and preserves local sessions.

Privacy guarantees:

- Auth integration does not read or sync `tokenforge-approved-locations.local.json`.
- Approved locations remain local-only and are never included in auth or sync requests.
- Sync payloads still go through `SafeSyncMapper` and the client privacy guard before network send.
- Auth logs include only endpoint name, status code, safe error code, and duration metadata.
- Passwords, access tokens, refresh tokens, request bodies, response bodies, raw paths, prompts, responses, commands, filenames, repo names, branch names, usernames, source snippets, secrets, raw logs, and raw Git output are never logged or displayed.

EditMode tests:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform editmode \
  -testResults /tmp/tokenforge-editmode-results.xml \
  -logFile /tmp/tokenforge-unity-test.log
```

Contract bundle export and server validation:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput /tmp/unity-safe-sync-contract-v1.bundle.json \
  -quit \
  -logFile /tmp/tokenforge-contract-export.log

cd /path/to/server
npm run client-contract:validate -- --bundle /tmp/unity-safe-sync-contract-v1.bundle.json
```

Optional manual auth/sync integration is replaced by the Phase 12 live auth/sync test flow below.

Known limitations:

- Schema version 1 only.
- No background sync.
- No conflict resolution.
- No tombstone sync.
- Token refresh depends on the server `/auth/refresh` endpoint.
- macOS Keychain is implemented for production runtime; non-macOS/editor fallback should use injected stores and must not be described as secure storage.
- No automatic retry queue.

## Phase 12 Account Hardening and Manual Live Auth/Sync Tests

Phase 12 hardens account-management behavior and adds opt-in live integration coverage without changing the local-first privacy boundary.

Auth/account flow:

- Signup, login, logout, and `users/me` are wired through `AuthSessionService` and testable without UI.
- Signup/login store returned access and refresh tokens only through `ISecureTokenStore`; macOS runtime uses Keychain, tests use `InMemoryTokenStore`.
- Signup/login do not trigger sync. Safe Sync still runs only from explicit user actions.
- `LoadCurrentUserAsync()` calls `GET /api/v1/users/me`; on `401`/expired access token it refreshes once, retries `users/me` once, then marks the session expired if refresh or retry fails.
- Logout clears secure token storage and may clear remote UI cache, but it does not delete local saved safe sessions or local-only approved locations.
- Auth failures, refresh failures, and server unavailability do not delete local safe sessions.
- UI state distinguishes logged out, logging in, signing up, refreshing, logged in, expired/auth-required, failed, and server unavailable states.
- Auth status displays only state, safe error code, user summary, and token expiration time. It never displays passwords, access tokens, refresh tokens, request bodies, or response bodies.

Safe auth error codes:

```text
AUTH_REQUIRED
INVALID_CREDENTIALS
EMAIL_ALREADY_EXISTS
TOKEN_EXPIRED
REFRESH_FAILED
SESSION_EXPIRED
SERVER_UNAVAILABLE
NETWORK_TIMEOUT
VALIDATION_FAILED
UNKNOWN_AUTH_ERROR
```

Manual live auth/sync tests are opt-in only. They are skipped by default in normal EditMode runs and require:

```text
TOKENFORGE_LIVE_INTEGRATION_ENABLED=true
TOKENFORGE_LIVE_BASE_URL=http://localhost:3000
TOKENFORGE_LIVE_EMAIL=<test user email>
TOKENFORGE_LIVE_PASSWORD=<test user password>
TOKENFORGE_LIVE_SIGNUP_EMAIL=<optional signup test email>
TOKENFORGE_LIVE_SIGNUP_PASSWORD=<optional signup test password>
TOKENFORGE_LIVE_ALLOW_SIGNUP=true
```

Run normal EditMode tests:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform editmode \
  -testResults /tmp/tokenforge-editmode-results.xml \
  -logFile /tmp/tokenforge-unity-test.log
```

Export the Safe Sync contract bundle:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput /tmp/unity-safe-sync-contract-v1.bundle.json \
  -quit \
  -logFile /tmp/tokenforge-contract-export.log
```

Validate the bundle against TokenForgeCoreServer:

```bash
cd /path/to/server
npm run client-contract:validate -- --bundle /tmp/unity-safe-sync-contract-v1.bundle.json
```

Run manual live auth/sync tests:

```bash
TOKENFORGE_LIVE_INTEGRATION_ENABLED=true \
TOKENFORGE_LIVE_BASE_URL=http://localhost:3000 \
TOKENFORGE_LIVE_EMAIL=test@example.com \
TOKENFORGE_LIVE_PASSWORD='replace-with-test-password' \
scripts/run-live-auth-sync-tests.sh
```

Local server checklist:

- Start TokenForgeCoreServer.
- Ensure the database is migrated.
- Ensure auth endpoints are enabled.
- Create or provide a dedicated test user.
- Confirm Safe Sync health and activity-session endpoints are available.
- Run normal Unity EditMode tests.
- Export the client contract bundle.
- Validate the bundle against the server.
- Run manual live auth/sync tests only with explicit environment variables.

Live test safety behavior:

- Uses deterministic safe sample sessions only.
- Uses a `phase12-live-test-` client session ID prefix.
- Uses `SafeSyncMapper` and the client privacy guard before sending.
- Does not read real Git repositories, Claude logs, Codex logs, raw approved-location settings, or real approved-location data.
- Deletes only the remote session created by the test client session ID and reports cleanup failures with safe warning codes only.
- Does not print credentials, tokens, request bodies, response bodies, raw logs, or payloads.

Account deletion:

- Account deletion is not implemented in the Phase 12 client because no clear delete-account server contract is defined here.
- No fake delete-account UI is provided.

Known limitations:

- Schema version 1 only.
- No background sync.
- No conflict resolution.
- No tombstone sync.
- No retry queue.
- Live tests are opt-in only.
- Non-macOS/editor secure token store must be injected explicitly.

## Phase 13 Production UI Polish and Recovery Hardening

Phase 13 polishes the bootstrap account and Safe Sync UI while keeping Safe Sync explicit and privacy-safe.

Account UI behavior:

- Account state is shown as Logged Out, Signing Up, Logging In, Logged In, Refreshing Session, Session Expired, Auth Required, Server Unavailable, or Failed.
- User summary displays only safe server fields: display name, email, or user id. Access and refresh tokens are never displayed.
- Session summary displays only `Session active`, `Session expired`, or `Refresh required`.
- Login is described as required only for server sync; local analysis stays available while logged out.
- Sign Up, Log In, Refresh User, and Log Out buttons are disabled while related requests are in progress.
- Successful login/signup clears password UI input and does not start sync.
- Logout clears token state and remote session UI cache but preserves local saved sessions and approved locations.

Safe Sync UI behavior:

- Safe Sync state is shown as Idle, Checking Server, Server Ready, Auth Required, Syncing, Synced, Fetching Remote Sessions, Delete In Progress, Server Unavailable, or Failed.
- Check Server remains unauthenticated. Sync Safe Sessions, Fetch Remote Sessions, and Delete Selected Remote Session require auth.
- Safe Sync is only run from explicit button actions. There is no background sync, app-start sync, or post-login/signup auto-sync.
- UI copy states that only privacy-safe aggregate sessions are synced and that approved locations and raw local data are never synced.
- Sync result counts show accepted/rejected totals. Remote rows show only safe summaries: server session id for delete selection, source provider, day bucket, confidence, warning count, and category/bucket summaries.
- Request and response JSON, raw local paths, raw payloads, credentials, and tokens are never shown.

Recovery behavior:

- Secure token store failures are normalized to safe codes: `TOKEN_STORE_UNAVAILABLE`, `TOKEN_STORE_READ_FAILED`, `TOKEN_STORE_WRITE_FAILED`, `TOKEN_STORE_CLEAR_FAILED`, `TOKEN_STORE_CORRUPTED_SESSION`, and `TOKEN_STORE_UNSUPPORTED_PLATFORM`.
- Token-store read failures do not crash startup and do not attempt sync.
- Login/signup are not reported as fully successful unless secure token storage succeeds.
- Logout clears in-memory auth state even if secure token clearing reports a safe warning.
- The client never falls back to PlayerPrefs or plain-text token storage.
- Empty or malformed base URLs are rejected before network calls. Localhost/dev URLs remain allowed.
- Changing base URL while logged in marks auth as required instead of silently sending an old token to another server.
- Duplicate login/signup/refresh/logout/sync/fetch/delete actions are guarded and return `ALREADY_IN_PROGRESS` instead of starting duplicate API calls.

Preservation and privacy guarantees:

- Auth failures, token-store failures, logout, sync failures, server unavailable, invalid base URLs, and privacy-guard blocks do not delete local saved safe sessions.
- Logout does not delete approved locations.
- Approved locations remain local-only in `tokenforge-approved-locations.local.json` and are never synced.
- UI state, logs, and safe error messages must not contain passwords, access tokens, refresh tokens, full request/response bodies, raw sync payloads, raw local paths, prompts, responses, commands, filenames, repo names, branch names, usernames, source snippets, secrets, raw logs, or raw Git output.

Normal EditMode validation:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform editmode \
  -testResults /tmp/tokenforge-editmode-results.xml \
  -logFile /tmp/tokenforge-unity-test.log
```

Contract export:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput /tmp/unity-safe-sync-contract-v1.bundle.json \
  -quit \
  -logFile /tmp/tokenforge-contract-export.log
```

Optional server validation:

```bash
cd /path/to/server
npm run client-contract:validate -- --bundle /tmp/unity-safe-sync-contract-v1.bundle.json
```

Optional live test remains opt-in:

```bash
TOKENFORGE_LIVE_INTEGRATION_ENABLED=true \
TOKENFORGE_LIVE_BASE_URL=http://localhost:3000 \
TOKENFORGE_LIVE_EMAIL=test@example.com \
TOKENFORGE_LIVE_PASSWORD='replace-with-test-password' \
scripts/run-live-auth-sync-tests.sh
```

Known limitations:

- Schema version 1 only.
- No background sync.
- No conflict resolution.
- No tombstone sync.
- No retry queue.
- Live tests are opt-in only.
- Non-macOS/editor secure token store must be injected explicitly.
- Prefab UI is improved but not final art.
- Script-built bootstrap UI fallback has been removed; the Bootstrap scene and `BootstrapRoot.prefab` are the supported startup path.

## Phase 14 Prefab UI and PlayMode Coverage

Phase 14 moves the bootstrap dashboard into authored Unity UI prefabs backed by thin binder components. The domain, parser, auth, sync, repository, mapper, and privacy logic remain in the existing application services and view models.

Prefab structure:

```text
Assets/_Project/Prefabs/UI/
  BootstrapRoot.prefab
  AccountPanel.prefab
  ActivityAnalysisPanel.prefab
  ApprovedLocationsPanel.prefab
  ReviewPanel.prefab
  SafeSyncPanel.prefab
  RecentSessionsPanel.prefab
  ReviewAndRecentSessionsPanel.prefab
  PrivacyNoticePanel.prefab
```

Binder components:

- `BootstrapRootView` binds the root prefab, validates panel references, owns scroll layout refresh, and fans render calls out to panels.
- `AccountPanelView` handles signup/login/logout/refresh-user UI events and clears password input on successful auth.
- `ActivityAnalysisPanelView` handles Git and agent location selection, provider choice, analysis settings, and explicit analyze actions.
- `ApprovedLocationsPanelView` handles local-only approved-location add/use/disable/remove controls and displays aliases/provider only.
- `ReviewPanelView` handles safe aggregate review save/discard controls.
- `SafeSyncPanelView` handles explicit health/sync/fetch/delete actions and disables request buttons while sync work is in progress.
- `RecentSessionsPanelView` renders local and remote safe summaries only.
- `PrivacyNoticePanelView` keeps aggregate-only/local-only/raw-data guarantees visible.

`AppBootstrapper` instantiates or locates `BootstrapRoot.prefab` and injects the existing `ApprovedActivityAnalysisViewModel`. The script-built bootstrap UI fallback has been removed; missing prefab UI now fails safely with a safe asset-level error.

PlayMode coverage:

- Prefab loading, panel existence, required serialized references, scroll root wiring, and safe broken-reference validation.
- Account button states, login-in-progress disabling, password clearing, logout/local availability, and no login-triggered sync.
- Safe Sync health while logged out, auth-required sync controls, explicit sync-only behavior, duplicate-click guard coverage, accepted/rejected count rendering, and safe server-unavailable messages.
- Activity analysis button states, approved-location alias-only display, review ready save/discard behavior, and agent provider selection.
- Privacy notice copy and visible-text scans for passwords, tokens, raw paths, payload fragments, prompts, responses, commands, source snippets, and approved-location raw paths.

PlayMode tests use fake auth/sync services, fake repositories, fake pickers, and deterministic safe data. They do not require a live server, real credentials, real Git repositories, real Claude/Codex logs, or Keychain access.

Normal PlayMode validation:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform playmode \
  -testResults /tmp/tokenforge-playmode-results.xml \
  -logFile /tmp/tokenforge-unity-playmode.log
```

Normal EditMode validation:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform editmode \
  -testResults /tmp/tokenforge-editmode-results.xml \
  -logFile /tmp/tokenforge-unity-editmode.log
```

Privacy UI guarantees:

- Safe Sync remains aggregate-only and explicit user action only.
- Signup/login/session restore do not auto-sync.
- Approved locations remain local-only in `tokenforge-approved-locations.local.json` and are never synced.
- UI labels display safe aliases, provider names, state labels, safe counts, safe error codes, and safe summaries only.
- Passwords, access tokens, refresh tokens, raw request/response JSON, raw payloads, raw local paths, approved-location raw paths, repo names, branch names, filenames, prompts, responses, commands, usernames, source snippets, secrets, raw logs, and raw Git output are not displayed or logged.

Known limitations:

- Schema version 1 only.
- No background sync.
- No conflict resolution.
- No tombstone sync.
- No retry queue.
- Live tests are opt-in only.
- UI is prefab-based but not final art/design.
- Script-built bootstrap UI fallback has been removed.
- Non-macOS/editor secure token store must be injected explicitly.

## Phase 15 Prefab Polish, Scene Smoke, and Fallback Preparation

Phase 15 improves the authored prefab UI hierarchy and accessibility without moving business logic out of the existing view model and service layers. The UI now has a clear `TokenForge` title, the subtitle `Privacy-safe developer activity companion`, section headers for Account, Local Analysis, Approved Local Locations, Review, Safe Sync, Recent Sessions, and Privacy, visible field labels, grouped action buttons, stronger empty states, and persistent privacy helper copy.

Prefab UI structure remains under:

```text
Assets/_Project/Prefabs/UI/
  BootstrapRoot.prefab
  AccountPanel.prefab
  ActivityAnalysisPanel.prefab
  ApprovedLocationsPanel.prefab
  ReviewPanel.prefab
  SafeSyncPanel.prefab
  RecentSessionsPanel.prefab
  ReviewAndRecentSessionsPanel.prefab
  PrivacyNoticePanel.prefab
```

Bootstrap scene:

```text
Assets/_Project/Scenes/Bootstrap.unity
```

The bootstrap scene contains the prefab-backed `BootstrapRoot` under the scene canvas and an `AppBootstrapper` configured with safe defaults: prefab UI required, no auth session load on scene start, no safe smoke sample creation on scene start, and no automatic sync. `Assets/_Project/Editor/TokenForgeBootstrapSceneBuilder.cs` rebuilds the scene and prefab references.

Audio policy:

- TokenForge MVP does not use runtime audio. `com.unity.modules.audio` is intentionally absent from `UnityClient/Packages/manifest.json`, and `UnityClient/ProjectSettings/AudioManager.asset` keeps Unity audio disabled.
- The Bootstrap scene camera must not include an `AudioListener`. This avoids Unity deleting the component at editor/play startup with `AudioListener component deleted: Component belongs to a disabled built-in package.`
- If product audio becomes part of the client later, enable the built-in audio module first, then add the required listener/source/mixer components deliberately.

Fallback UI status:

- Removed in Phase 17.
- `AppBootstrapper` no longer exposes `allowFallbackUi` or fallback usage state.
- If `BootstrapRoot.prefab` is missing, startup logs a safe error and does not create a parallel script UI.

Phase 15 PlayMode coverage:

- `Phase15BootstrapSceneSmokePlayModeTests` loads the real `Bootstrap` scene, asserts prefab UI is active, verifies all main panels exist, confirms dependencies initialize, and checks default UI state.
- `Phase15UiAccessibilityPlayModeTests` checks visible labels, descriptive button text, masked password input, empty states, privacy notice copy, and visible text privacy scanning.
- `Phase15LayoutRegressionPlayModeTests` checks scroll root wiring, major panel parentage, default active panels, key default button states, status labels, password content type, and local/remote/approved-location containers.
- Phase 17 fallback removal tests verify missing-prefab startup fails safely without creating script UI.

UI privacy scanner:

- `UiVisibleTextScanner` collects runtime-visible Unity UI text and flags forbidden runtime fragments such as tokens, raw path markers, raw JSON fragments, API key markers, and absolute user paths.
- Tests additionally scan for representative sensitive values: passwords, access tokens, refresh tokens, raw local paths, repo names, branch names, commands, prompts/responses, source snippets, and approved-location raw paths.

Scene smoke PlayMode command:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform playmode \
  -testResults /tmp/tokenforge-playmode-results.xml \
  -logFile /tmp/tokenforge-unity-playmode.log
```

EditMode command:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform editmode \
  -testResults /tmp/tokenforge-editmode-results.xml \
  -logFile /tmp/tokenforge-unity-editmode.log
```

Contract export command:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput /tmp/unity-safe-sync-contract-v1.bundle.json \
  -quit \
  -logFile /tmp/tokenforge-contract-export.log
```

Optional server validation:

```bash
cd /path/to/server
npm run client-contract:validate -- --bundle /tmp/unity-safe-sync-contract-v1.bundle.json
```

Phase 15 privacy UI guarantees:

- Safe Sync remains aggregate-only and requires explicit user action.
- Scene load does not start auth requests, sync requests, background analysis, or smoke sample creation.
- Approved locations stay local-only in `tokenforge-approved-locations.local.json` and are never synced.
- The UI displays safe aliases, provider/state labels, safe counts, safe status messages, and safe aggregate summaries only.
- The UI does not display passwords, tokens, raw local paths, approved-location raw paths, raw payloads, prompts, responses, commands, filenames, repo names, branch names, usernames, raw logs, raw Git output, source snippets, secrets, request bodies, or response bodies.

Known Phase 15 limitations:

- Schema version 1 only.
- No background sync.
- No conflict resolution.
- No tombstone sync.
- No retry queue.
- Live tests are opt-in only.
- Prefab UI is improved but not final art.
- Script-built bootstrap UI fallback has been removed.
- Non-macOS/editor secure token store must be injected explicitly.

## Phase 16 macOS Build Smoke and Startup Hardening

Phase 16 added a repeatable local macOS build smoke path around the prefab-backed Bootstrap scene and tightened startup defaults. Phase 17 removes the temporary script-built bootstrap fallback entirely.

Bootstrap scene and prefab path:

```text
Assets/_Project/Scenes/Bootstrap.unity
Assets/_Project/Prefabs/UI/BootstrapRoot.prefab
```

macOS build smoke:

```bash
scripts/build-macos-smoke.sh
```

Defaults:

```text
Unity: /Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity
Build output: /tmp/tokenforge-macos-build/TokenForge.app
Build log: /tmp/tokenforge-macos-build.log
```

Override Unity or output paths when needed:

```bash
UNITY_PATH=/path/to/Unity \
BUILD_OUTPUT=/tmp/tokenforge-macos-build/TokenForge.app \
LOG_FILE=/tmp/tokenforge-macos-build.log \
scripts/build-macos-smoke.sh
```

Manual launch smoke:

```bash
scripts/run-macos-smoke.sh
```

Checklist:

- App launches without crash.
- Bootstrap scene appears.
- Account panel appears.
- Local Analysis panels appear.
- Safe Sync panel appears.
- Privacy notice appears.
- No sync starts automatically.
- No login starts automatically.
- No analysis starts automatically.
- Logged-out local features are visible.
- Health button is available.
- Sync/fetch/delete require auth.

Fallback status:

- `BootstrapRoot.prefab` is the only supported normal startup UI path.
- `allowFallbackUi`, fallback usage tracking, and script-built fallback creation have been removed.
- If `BootstrapRoot.prefab` is missing, startup logs a safe prefab error and does not create script UI.

Validation commands:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform editmode \
  -testResults /tmp/tokenforge-editmode-results.xml \
  -logFile /tmp/tokenforge-unity-editmode.log
```

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform playmode \
  -testResults /tmp/tokenforge-playmode-results.xml \
  -logFile /tmp/tokenforge-unity-playmode.log
```

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput /tmp/unity-safe-sync-contract-v1.bundle.json \
  -quit \
  -logFile /tmp/tokenforge-contract-export.log
```

```bash
cd /path/to/server
npm run client-contract:validate -- --bundle /tmp/unity-safe-sync-contract-v1.bundle.json
```

Optional live auth/sync tests remain opt-in:

```bash
TOKENFORGE_LIVE_INTEGRATION_ENABLED=true \
TOKENFORGE_LIVE_BASE_URL=http://localhost:3000/api/v1 \
TOKENFORGE_LIVE_EMAIL=<email> \
TOKENFORGE_LIVE_PASSWORD=<password> \
scripts/run-live-auth-sync-tests.sh
```

Build artifact ignore policy:

- Build output stays local under `/tmp/tokenforge-macos-build/` by default.
- Repository-local `Builds/`, `build/`, `DerivedData/`, generated contract JSON, approved-location local JSON, and token/session debug files are ignored.
- Prefabs, the Bootstrap scene, test sources, scripts, and Unity `.meta` files remain source assets.

Known Phase 16 limitations:

- Schema version 1 only.
- No background sync.
- No conflict resolution.
- No tombstone sync.
- No retry queue.
- Live tests are opt-in only.
- Developer ID signing and notarization still require release-machine credentials.
- Prefab UI is improved but not final art.
- Non-macOS/editor secure token store must be injected explicitly.

## Phase 17 Fallback Removal and macOS Release Preparation

Phase 17 removes the remaining dev-only script UI fallback. The supported startup path is now `Assets/_Project/Scenes/Bootstrap.unity` with `Assets/_Project/Prefabs/UI/BootstrapRoot.prefab`; missing prefab UI fails safely and does not create a parallel script-built UI.

Release smoke commands:

```bash
scripts/build-macos-smoke.sh
scripts/run-macos-smoke.sh
scripts/package-macos-smoke.sh
```

Local ad-hoc signing smoke:

```bash
TOKENFORGE_MACOS_SIGN_MODE=adhoc \
TOKENFORGE_MACOS_SIGN_IDENTITY="-" \
scripts/sign-macos-app.sh

scripts/verify-macos-signing.sh
```

Developer ID signing template. Set the signing identity in the release shell; values are intentionally omitted from documentation:

```bash
scripts/sign-macos-app.sh
```

Notarization template:

```bash
scripts/package-macos-smoke.sh

TOKENFORGE_NOTARIZE=true \
NOTARYTOOL_KEYCHAIN_PROFILE="tokenforge-notary" \
PACKAGE_PATH=/tmp/tokenforge-release/TokenForge.zip \
scripts/notarize-macos-app.sh

scripts/staple-macos-app.sh
```

Environment variables:

- `APP_PATH`: built `.app` path, default `/tmp/tokenforge-macos-build/TokenForge.app`.
- `TOKENFORGE_MACOS_SIGN_MODE`: `adhoc` or `developer-id`.
- `TOKENFORGE_MACOS_SIGN_IDENTITY`: `-` for ad-hoc or a Developer ID identity for release.
- `TOKENFORGE_MACOS_BUNDLE_ID`: optional signing identifier override.
- `TOKENFORGE_MACOS_ENTITLEMENTS_PATH`: defaults to `BuildSupport/macOS/TokenForge.entitlements`.
- `APPLE_ID`, `TEAM_ID`, `APP_SPECIFIC_PASSWORD`, or `NOTARYTOOL_KEYCHAIN_PROFILE`: notarization credentials; normal validation does not require them.

Secrets policy:

- Do not commit certificates, provisioning profiles, API keys, Apple ID credentials, app-specific passwords, notary credentials, token/session files, generated local data, build outputs, `.dmg`, `.pkg`, or `.zip` release artifacts.
- Build, signing, notarization, and package scripts must not print secrets, request bodies, response bodies, raw sync payloads, raw local paths, tokens, or passwords.
- The package smoke script rejects approved-location files, generated client-contract JSON, token/session debug files, and local token/session artifacts inside the zip.

macOS entitlements:

- `BuildSupport/macOS/TokenForge.entitlements` is intentionally minimal and currently grants no privileged entitlements.
- Keychain usage for the non-sandboxed macOS runtime does not require a committed Keychain access group entitlement.
- Network client access is not declared because the app is not sandboxed in this preparation phase.
- Hardened runtime is applied by `codesign --options runtime` in the signing script.
- Bundle identifier, app name, version, and build number remain Unity build metadata; release builds should verify them in `Info.plist` before notarization.

Manual release checklist:

- Run EditMode and PlayMode tests.
- Export the Safe Sync contract bundle and validate it against the server.
- Run macOS build smoke and launch smoke.
- Run package smoke and verify `/tmp/tokenforge-release/TokenForge.zip`.
- Sign with Developer ID credentials only on a release machine.
- Submit notarization explicitly with `TOKENFORGE_NOTARIZE=true`.
- Staple and verify the notarized app before distribution.

Known Phase 17 limitations:

- Prepared for signing/notarization, but not notarized unless credentials are provided and notarization is explicitly enabled.
- Schema version 1 only.
- No background sync.
- No conflict resolution.
- No tombstone sync.
- No retry queue.
- Live tests are opt-in only.
- Prefab UI is improved but not final art.
- Non-macOS/editor secure token store must be injected explicitly.

See `Docs/macos-release.md` for the release preparation workflow.

## Project Structure

```text
UnityClient/
  Assets/_Project/Prefabs/UI/
  Assets/_Project/Scripts/
    Domain/
    Growth/
    Agents/
    Git/
    Privacy/
    Persistence/
    Sync/
    Platform/
    UI/
    MiniGame/
    Character/
    Common/
  Assets/_Project/Tests/EditMode/
  Assets/_Project/Tests/PlayMode/
  Packages/
  ProjectSettings/
Docs/
ArtSource/
Tools/
```

## Running

Open `UnityClient/` in Unity Editor. This repository contains a minimal Unity project skeleton and C# scripts; it was not generated by Unity Editor in this environment, so Unity may create additional local project metadata on first open.

Recommended editor version: Unity 2022.3 LTS or newer.

## JSON Note

`JsonSaveDataStore` currently uses `System.Text.Json` because no Unity package registry was available in this environment. If the target Unity version lacks compatible `System.Text.Json` support, add Newtonsoft Json for Unity and switch the serializer behind `ISaveDataStore` without changing Domain models.

## Tests

EditMode tests are under:

```text
UnityClient/Assets/_Project/Tests/EditMode/
```

Run them from Unity Test Runner:

1. Open `UnityClient/` in Unity.
2. Open `Window > General > Test Runner`.
3. Select `EditMode`.
4. Run all tests.

From a shell with Unity installed, the equivalent batch-mode command is:

```bash
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath "$PWD/UnityClient" \
  -runTests \
  -testPlatform editmode \
  -testResults /tmp/tokenforge-editmode-results.xml \
  -logFile /tmp/tokenforge-unity-test.log
```

With Unity Test Framework 1.1.x, do not pass `-quit`; the test runner exits through its own callback after saving results.

## Unity Editor Follow-Up

- Let Unity generate `.meta` files.
- Confirm assembly definitions resolve in the selected Unity version.
- Confirm `System.Text.Json` availability or replace with Newtonsoft Json for Unity.
- Maintain Bootstrap/Main scenes in `Assets/_Project/Scenes`.
- Continue final art polish on the prefab UI.

## Next Phase

- Run a real Developer ID signing and notarization pass on release hardware.
- Add explicit local-only path reveal/copy controls only if needed for management/debugging.
- Harden persistence migrations.

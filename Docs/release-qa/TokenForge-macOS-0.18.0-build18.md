# TokenForge macOS 0.18.0 build 18 Release QA

Date: 2026-05-15

## Build

- Product: TokenForge
- Bundle identifier: com.tokenforge.client
- Version: 0.18.0
- Build: 18
- Local ad-hoc package: `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18.zip`
- Credentialed notarized package target: `/tmp/tokenforge-release/TokenForge-macOS-0.18.0-build18-notarized.zip`
- Machine notes: local macOS validation environment; Apple release credentials were not configured.

## Signing, Notarization, Stapling, Gatekeeper

- Signing mode run locally: ad-hoc.
- Developer ID signing status: not run. Safe prerequisite failure was confirmed when `TOKENFORGE_MACOS_SIGN_IDENTITY` was unset.
- Notarization status: not run. Safe prerequisite failure was confirmed when no notary keychain profile or direct Apple credential set was configured.
- Stapling status: not run for a notarized app because notarization was unavailable.
- Gatekeeper status: ad-hoc package is not expected to pass full `spctl` assessment. Notarized Developer ID Gatekeeper verification remains a credentialed release-machine item.

## Package And Privacy

- Package content: contains `TokenForge.app`.
- Package metadata: TokenForge, `com.tokenforge.client`, version `0.18.0`, build `18`.
- Final icon: configured as `Assets/_Project/Art/AppIcon/TokenForgeReleaseIcon.png`.
- Placeholder icon: absent from package scan.
- Release artifact privacy scan: passed.
- Forbidden generated/local data: not present in package scan.

## Automated Validation

- Release metadata validation: passed.
- EditMode tests: 209 passed, 0 failed, 2 skipped.
- PlayMode tests: 21 passed, 0 failed.
- Safe Sync contract bundle export: passed.
- Server contract validation: passed.
- macOS build smoke: passed.
- package smoke: passed.
- release prep check: passed.
- release candidate package: passed.
- clean install smoke: structural checks passed.
- update migration smoke: synthetic fixture checks passed.

## Clean Install GUI QA

Status: not run manually in this local pass.

Automated structural clean-install smoke remains required and checks package extraction, executable metadata, and absence of local data, recovery files, `.env`, token/session files, logs, and sensitive markers. The GUI checklist still needs a credentialed release-machine/manual run against the packaged app:

- Gatekeeper behavior.
- Launch without crash.
- Bootstrap, Account, Local Analysis, Approved Local Locations, Review, Safe Sync, Recent Sessions, and Privacy notice visible.
- No automatic login, sync, or analysis.
- Clean approved locations and saved sessions.
- Login required only for server sync.
- Local analysis visible while logged out.
- Health check, sync, fetch, and delete remain explicit.
- Password input remains masked.
- No tokens, passwords, raw paths, raw payloads, prompts, responses, commands, or source snippets visible in UI or logs.
- Quit and reopen.

## Update Migration GUI QA

Status: not run manually in this local pass.

Automated synthetic fixture migration smoke remains required. Packaged GUI migration QA is limited because a public release-build isolated data-dir override is not documented in this phase. The remaining manual release-machine checklist is:

- Seed legacy/missing-schema safe save data with synthetic data only.
- Seed legacy approved-location settings with synthetic local-only paths.
- Launch packaged app with an isolated supported runtime data directory if available.
- Verify migration to schema v1.
- Verify unknown future schema and corrupt files fail safely without crash.
- Verify backup/recovery files stay local-only and are not packaged.
- Verify no sync starts automatically during migration.
- Verify logs do not expose raw approved-location paths or full save payloads.

## Commands Used

```bash
scripts/package-macos-release-candidate.sh
```

Credentialed release-machine command. Set signing and notary credential values in the release shell; values are intentionally omitted from documentation:

```bash
scripts/package-macos-release-candidate.sh
```

Alternative notary mode also keeps values out of documentation:

```bash
scripts/package-macos-release-candidate.sh
```

Post-notarization verification commands:

```bash
xcrun stapler staple /tmp/tokenforge-macos-build/TokenForge.app
xcrun stapler validate /tmp/tokenforge-macos-build/TokenForge.app
codesign --verify --deep --strict /tmp/tokenforge-macos-build/TokenForge.app
spctl --assess --type execute --verbose /tmp/tokenforge-macos-build/TokenForge.app
```

## Known Limitations

- Developer ID signing/notarization still requires a credentialed release machine.
- Persistence schema v1 only.
- Safe Sync schemaVersion 1 only.
- No background sync, conflict resolution, tombstone sync, or retry queue.
- Live tests remain opt-in.
- Non-macOS/editor secure token store still requires explicit injection.
- Manual clean-install GUI QA and manual update-migration GUI QA remain release-machine items for this pass.

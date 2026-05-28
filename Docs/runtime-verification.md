# TokenForge macOS Runtime Verification

Status: BLOCKED until Unity lock is cleared, a clean rebuild completes, LaunchServices works from a normal non-sandbox Terminal, and runtime/manual UI gates pass.

Runtime PASS must not be claimed from the current Codex sandbox/session. In this session, system apps and TokenForge both fail LaunchServices registration/open checks, so the failure is environment-wide rather than proven TokenForge-only.

## Current Evidence

- `UnityClient/Temp/UnityLockfile` exists.
- `lsof` showed Unity PID `41518` and `fileproviderd` PID `681` holding the lock.
- Sandbox denied process control, including `ps` and `kill -TERM 41518`.
- `scripts/build-macos-smoke.sh` exits `2` with `blocked: unity project lock`.
- `open /System/Applications/TextEdit.app`, `open /System/Applications/Calculator.app`, and `open /private/tmp/TokenForge-verify.app` failed with `Code=-10827 kLSNoExecutableErr`.
- `lsregister -f` failed for system apps and TokenForge with `-10822` from Spotlight.
- TokenForge direct executable reached Unity/AppKit and aborted at HIServices `_RegisterApplication`.
- The `TokenForge-2026-05-28-221500.ips` crash path does not include `libDesktopCompanionOverlay` or a native TokenForge export frame.
- `[RuntimeVerify][ENABLED]` and `[WindowsDump][LAUNCH_STABLE] phase=launch+15s` have not been observed.

## Manual Unity Lock Release

Do these steps outside the Codex sandbox, using the macOS GUI or a normal Terminal:

1. Quit Unity Editor completely.
2. Quit Unity Hub completely.
3. In Activity Monitor, end Unity PID `41518` if it is still alive.
4. End Unity Licensing Client related processes, then retry the lock check.
5. If `fileproviderd` still holds the lock, check whether the project is under iCloud, File Provider, Dropbox, OneDrive, or another sync-backed path.
6. If the path is sync-backed, copy the repo to a local-only path such as `~/Developer/TokenForge` or `~/Projects/TokenForge`.
7. Only after Unity/Hub/Licensing Client are fully stopped, remove `UnityClient/Temp/UnityLockfile` if it still exists.
8. Re-run the clean rebuild.

Do not delete `UnityLockfile` while Unity is still running. Do not ignore a `fileproviderd` holder. Do not claim Runtime PASS without a clean rebuild.

## Non-Sandbox Verification Commands

Prefer the scripted path:

```bash
cd /Users/hwangseokbeom/Documents/GitHub/TokenForge
scripts/preflight-runtime-environment.sh
scripts/verify-tokenforge-runtime.sh
```

Manual equivalent:

```bash
cd /Users/hwangseokbeom/Documents/GitHub/TokenForge

# 1. After manually quitting Unity/Hub, confirm the lock is gone.
lsof UnityClient/Temp/UnityLockfile || true
ls -la UnityClient/Temp/UnityLockfile || true

# 2. Only rebuild after the lock is cleared.
BUILD_OUTPUT="$PWD/UnityClient/builds/macOS/TokenForge.app" scripts/build-macos-smoke.sh

# 3. Prepare a verify app from the clean artifact.
rm -rf /private/tmp/TokenForge-verify.app
cp -R UnityClient/builds/macOS/TokenForge.app /private/tmp/TokenForge-verify.app
xattr -cr /private/tmp/TokenForge-verify.app
codesign --verify --deep --strict --verbose=4 /private/tmp/TokenForge-verify.app

# 4. Register and launch via LaunchServices.
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f /private/tmp/TokenForge-verify.app
TOKENFORGE_VERIFY_RUNTIME=1 open /private/tmp/TokenForge-verify.app --args -TokenForgeVerifyRuntime YES

# 5. Collect logs.
log stream --style compact --predicate 'process CONTAINS "TokenForge"' --level debug
```

## LaunchServices Environment Gate

Use system apps as the comparison baseline:

```bash
open /System/Applications/TextEdit.app
open /System/Applications/Calculator.app
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f /System/Applications/TextEdit.app
```

If system apps and TokenForge both fail `open` or `lsregister -f`, treat LaunchServices as unavailable for that session. TokenForge `open` failure is not proven TokenForge-only in that condition.

## Direct Executable Classification

Direct executable launch is supplementary evidence only. TokenForge currently aborts at HIServices `_RegisterApplication`, while system GUI direct executable comparison is blocked by sandbox launch constraints and CODESIGNING launch constraint violations.

Keep this classification until a normal Terminal run proves otherwise:

- TokenForge direct executable crash: unresolved.
- `libDesktopCompanionOverlay` involvement: no, based on the current crash log.
- Primary runtime path: normal `open` from a non-sandbox Terminal.

## Runtime PASS Criteria

Runtime PASS requires all four items to be `YES`:

| Item | Required |
| --- | --- |
| LaunchServices `open` succeeds | YES |
| `[RuntimeVerify][ENABLED]` appears | YES |
| `[WindowsDump][LAUNCH_STABLE] phase=launch+15s` appears | YES |
| Dashboard/overlay manual test passes | YES |

If any item is `NO`, the result is:

Runtime PASS is NOT claimed.

## Manual UI Checklist

| Item | Required Evidence |
| --- | --- |
| Verification mode enabled | `[RuntimeVerify][ENABLED]` |
| Report Issue does not auto-present on launch | `reportIssueAutoPresent=false route=manualOnly` |
| Crash recovery does not block runtime verification | `[CrashRecovery][SUPPRESSED_REPORT_UI] reason=verificationMode` |
| No auto reopen loop after launch | `[DashboardLifecycle][SUPPRESS_REOPEN] reason=verificationMode` |
| No watchdog recreate loop during warmup | `[OverlayWatchdog][SUPPRESSED] reason=verificationModeWarmup` |
| Windows stable for 15 seconds before dashboard test | `[WindowsDump][LAUNCH_STABLE] phase=launch+15s` |
| App can be controlled after launch | Manual observation |
| Dashboard opens only by explicit action | Manual observation |
| Dashboard closes with one close action | Manual observation |
| Overlay drag does not affect dashboard layout | Manual observation |

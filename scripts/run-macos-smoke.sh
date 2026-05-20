#!/usr/bin/env bash
set -euo pipefail

APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
WAIT_SECONDS="${WAIT_SECONDS:-8}"
PLAYER_LOG="${PLAYER_LOG:-${HOME}/Library/Logs/TokenForge/TokenForge/Player.log}"

if [[ ! -d "${APP_PATH}" ]]; then
  echo "Built app was not found: ${APP_PATH}" >&2
  echo "Run scripts/build-macos-smoke.sh first, or set APP_PATH to a built TokenForge.app." >&2
  exit 1
fi

open -n "${APP_PATH}"
sleep "${WAIT_SECONDS}"

window_result="not checked"
if command -v osascript >/dev/null 2>&1; then
  window_result="$(osascript <<'APPLESCRIPT' 2>/dev/null || true
tell application "System Events"
  set matches to {}
  repeat with p in (application processes whose name is "TokenForge")
    repeat with w in windows of p
      if name of w contains "TokenForge" then set end of matches to name of w
    end repeat
  end repeat
  if (count of matches) > 0 then
    return "found: " & item 1 of matches
  else
    return "missing"
  end if
end tell
APPLESCRIPT
)"
fi

log_result="missing"
if [[ -f "${PLAYER_LOG}" ]]; then
  required_patterns=(
    "status item installed"
    "AppKit dashboard bridge installed"
    "productUI=disabled reason=nativeShell"
    "state updated"
    "action callback registered"
  )
  log_result="ok"
  for pattern in "${required_patterns[@]}"; do
    if ! grep -q "${pattern}" "${PLAYER_LOG}"; then
      log_result="missing pattern: ${pattern}"
      break
    fi
  done
fi

process_result="running"
if ! pgrep -x TokenForge >/dev/null 2>&1; then
  process_result="not running"
fi

echo "TokenForge macOS launch smoke"
echo "App path: ${APP_PATH}"
echo "Player log: ${PLAYER_LOG}"
echo "Process: ${process_result}"
echo "Native window: ${window_result}"
echo "Native log checks: ${log_result}"
echo "Result: launched"
echo "Manual checklist:"
echo "- App launches without crash and opens the AppKit TokenForge dashboard."
echo "- Unity BootstrapRoot product UI is not visible in the macOS Player path."
echo "- Dashboard/sidebar/buttons render without clipped text or overlapping cards."
echo "- Dashboard, Repository, Codex Agent, Activity, Settings, Homepage, Report Issue, and Quit each trigger native action handling."
echo "- Settings toggles update companion visible, wander, and click reaction state or show an intentional disabled Coming soon state."
echo "- Player.log has no Auto Layout constraint warning."

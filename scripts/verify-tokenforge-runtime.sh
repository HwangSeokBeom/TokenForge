#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
BUILD_OUTPUT="${BUILD_OUTPUT:-${REPO_ROOT}/UnityClient/builds/macOS/TokenForge.app}"
VERIFY_APP_PATH="${VERIFY_APP_PATH:-/private/tmp/TokenForge-verify.app}"
VERIFY_LOG_DIR="${VERIFY_LOG_DIR:-/private/tmp/tokenforge-runtime-verify}"
LSREGISTER="${LSREGISTER:-/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister}"
LOG_STREAM_FILE="${VERIFY_LOG_DIR}/tokenforge-log-stream.log"
LOG_SHOW_FILE="${VERIFY_LOG_DIR}/tokenforge-log-show.log"
HASH_REPORT="${VERIFY_LOG_DIR}/bundle-hashes.sha256"
MANUAL_UI_VERIFIED="${MANUAL_UI_VERIFIED:-NO}"

mkdir -p "${VERIFY_LOG_DIR}"

section() {
  printf '\n== %s ==\n' "$1"
}

require_log() {
  local pattern="$1"
  local label="$2"
  if grep -Fq "${pattern}" "${LOG_STREAM_FILE}" "${LOG_SHOW_FILE}" 2>/dev/null; then
    printf 'PASS: %s (%s)\n' "${label}" "${pattern}"
    return 0
  fi
  printf 'FAIL: %s missing (%s)\n' "${label}" "${pattern}"
  return 1
}

section "Preflight"
RUN_TOKENFORGE_OPEN_PROBE=false REQUIRE_APP_INTEGRITY=false APP_BUNDLE_PATH="${BUILD_OUTPUT}" "${SCRIPT_DIR}/preflight-runtime-environment.sh"

section "Clean Rebuild"
if ! BUILD_OUTPUT="${BUILD_OUTPUT}" CLEAN_BUILD=true "${SCRIPT_DIR}/build-macos-smoke.sh"; then
  echo "Clean rebuild failed or was blocked."
  echo "Runtime PASS is NOT claimed."
  exit 2
fi

if [[ ! -d "${BUILD_OUTPUT}" ]]; then
  echo "Build output is missing after clean rebuild: ${BUILD_OUTPUT}" >&2
  echo "Runtime PASS is NOT claimed." >&2
  exit 5
fi

section "Build Artifact Hash Report"
: > "${HASH_REPORT}"
if [[ -f "${BUILD_OUTPUT}/Contents/MacOS/TokenForge" ]]; then
  shasum -a 256 "${BUILD_OUTPUT}/Contents/MacOS/TokenForge" >> "${HASH_REPORT}"
fi
find "${BUILD_OUTPUT}/Contents" -type f \( -name '*.dll' -o -name '*.dylib' -o -name '*.bundle' \) -print0 2>/dev/null \
  | xargs -0 shasum -a 256 >> "${HASH_REPORT}" 2>/dev/null || true
cat "${HASH_REPORT}"

section "Build Artifact Freshness Gate"
BUILD_OUTPUT="${BUILD_OUTPUT}" APP_BUNDLE_PATH="${BUILD_OUTPUT}" "${SCRIPT_DIR}/verify-macos-build-artifacts.sh"

section "Prepare Verify App"
rm -rf "${VERIFY_APP_PATH}"
cp -R "${BUILD_OUTPUT}" "${VERIFY_APP_PATH}"
xattr -cr "${VERIFY_APP_PATH}"
if ! codesign --verify --deep --strict --verbose=4 "${VERIFY_APP_PATH}"; then
  echo "codesign strict verification failed for ${VERIFY_APP_PATH}."
  echo "Runtime PASS is NOT claimed."
  exit 8
fi

section "LaunchServices Registration"
if ! "${LSREGISTER}" -f "${VERIFY_APP_PATH}"; then
  echo "LaunchServices registration failed for ${VERIFY_APP_PATH}."
  echo "Runtime PASS is NOT claimed."
  exit 9
fi

section "Launch Runtime Verification"
: > "${LOG_STREAM_FILE}"
: > "${LOG_SHOW_FILE}"
log stream --style compact --predicate 'process CONTAINS "TokenForge"' --level debug > "${LOG_STREAM_FILE}" 2>&1 &
LOG_STREAM_PID=$!
cleanup() {
  if kill -0 "${LOG_STREAM_PID}" >/dev/null 2>&1; then
    kill "${LOG_STREAM_PID}" >/dev/null 2>&1 || true
    wait "${LOG_STREAM_PID}" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

if ! TOKENFORGE_VERIFY_RUNTIME=1 open "${VERIFY_APP_PATH}" --args -TokenForgeVerifyRuntime YES; then
  echo "LaunchServices open failed for ${VERIFY_APP_PATH}."
  echo "Runtime PASS is NOT claimed."
  exit 10
fi
sleep "${VERIFY_RUNTIME_WAIT_SECONDS:-30}"
log show --style compact --predicate 'process CONTAINS "TokenForge"' --last 2m --debug > "${LOG_SHOW_FILE}" 2>&1 || true
cleanup
trap - EXIT

section "Required Runtime Logs"
MISSING=0
require_log "[RuntimeVerify][ENABLED]" "verification mode enabled" || MISSING=1
require_log "[CrashRecovery][SUPPRESSED_REPORT_UI] reason=verificationMode" "crash recovery report UI suppressed" || MISSING=1
require_log "reportIssueAutoPresent=false route=manualOnly" "Report Issue remains manual-only" || MISSING=1
require_log "[DashboardLifecycle][SUPPRESS_REOPEN] reason=verificationMode" "dashboard reopen suppressed" || MISSING=1
require_log "[OverlayWatchdog][SUPPRESSED] reason=verificationModeWarmup" "overlay watchdog suppressed during warmup" || MISSING=1
require_log "[WindowsDump][LAUNCH_STABLE] phase=launch+15s" "windows stable after launch" || MISSING=1

section "Result"
echo "Log stream file: ${LOG_STREAM_FILE}"
echo "Log show file: ${LOG_SHOW_FILE}"
echo "Hash report: ${HASH_REPORT}"

if [[ ${MISSING} -ne 0 ]]; then
  echo "Runtime PASS is NOT claimed."
  exit 6
fi

if [[ "${MANUAL_UI_VERIFIED}" != "YES" ]]; then
  echo "Automated runtime log gates passed, but manual dashboard/overlay verification is still required."
  echo "Set MANUAL_UI_VERIFIED=YES only after completing the documented manual UI checks."
  echo "Runtime PASS is NOT claimed."
  exit 7
fi

echo "Runtime verification gates and manual UI verification are complete."

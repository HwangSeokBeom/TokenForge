#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"
BUILD_OUTPUT="${BUILD_OUTPUT:-/private/tmp/tokenforge-macos-build/TokenForge.app}"
BUILD_OUTPUT_DIR="$(dirname "${BUILD_OUTPUT}")"
BUILD_STATUS_FILE="${BUILD_STATUS_FILE:-${BUILD_OUTPUT_DIR}/tokenforge-build-status.env}"
VERIFY_APP_PATH="${VERIFY_APP_PATH:-/private/tmp/TokenForge-verify.app}"
VERIFY_LOG_DIR="${VERIFY_LOG_DIR:-/private/tmp/tokenforge-runtime-verify}"
VERIFY_RUNTIME_WAIT_SECONDS="${VERIFY_RUNTIME_WAIT_SECONDS:-60}"
VERIFY_SKIP_REBUILD="${VERIFY_SKIP_REBUILD:-false}"
VERIFY_RESIGN_APP="${VERIFY_RESIGN_APP:-true}"
VERIFY_CRASH_DETECTION_SLOP_SECONDS="${VERIFY_CRASH_DETECTION_SLOP_SECONDS:-10}"
VERIFY_CRASH_REPORT_WAIT_SECONDS="${VERIFY_CRASH_REPORT_WAIT_SECONDS:-10}"
LSREGISTER="${LSREGISTER:-/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister}"
LOG_STREAM_FILE="${VERIFY_LOG_DIR}/tokenforge-log-stream.log"
LOG_SHOW_FILE="${VERIFY_LOG_DIR}/tokenforge-log-show.log"
DIRECT_LAUNCH_LOG_FILE="${VERIFY_LOG_DIR}/tokenforge-direct-launch.log"
HASH_REPORT="${VERIFY_LOG_DIR}/bundle-hashes.sha256"
CRASH_REPORT_FILE="${VERIFY_LOG_DIR}/crash-files-after-launch.txt"
CRASH_ARTIFACT_DIR="${VERIFY_LOG_DIR}/crash-artifacts"
MANUAL_UI_VERIFIED="${MANUAL_UI_VERIFIED:-NO}"
VERIFY_LAUNCH_MODE="${VERIFY_LAUNCH_MODE:-auto}"
LOG_STREAM_PID=""
DIRECT_LAUNCH_PID=""

mkdir -p "${VERIFY_LOG_DIR}" "${CRASH_ARTIFACT_DIR}"

section() {
  printf '\n== %s ==\n' "$1"
}

build_status_value() {
  local key="$1"
  [[ -f "${BUILD_STATUS_FILE}" ]] || return 1
  awk -F= -v key="${key}" '$1 == key {print substr($0, index($0, "=") + 1); exit}' "${BUILD_STATUS_FILE}"
}

require_log() {
  local pattern="$1"
  local label="$2"
  if grep -Fq "${pattern}" "${LOG_STREAM_FILE}" "${LOG_SHOW_FILE}" "${DIRECT_LAUNCH_LOG_FILE}" 2>/dev/null; then
    printf 'PASS: %s (%s)\n' "${label}" "${pattern}"
    return 0
  fi
  printf 'FAIL: %s missing (%s)\n' "${label}" "${pattern}"
  return 1
}

app_executable() {
  local plist="${VERIFY_APP_PATH}/Contents/Info.plist"
  local executable_name="TokenForge"
  if [[ -f "${plist}" && -x /usr/libexec/PlistBuddy ]]; then
    executable_name="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "${plist}" 2>/dev/null || echo TokenForge)"
  fi
  printf '%s/Contents/MacOS/%s' "${VERIFY_APP_PATH}" "${executable_name}"
}

latest_file_epoch() {
  local root="$1"
  local pattern="$2"
  [[ -d "${root}" ]] || return 1
  find "${root}" -name "${pattern}" -type f -print0 2>/dev/null \
    | xargs -0 stat -f '%m %N' 2>/dev/null \
    | sort -nr \
    | head -1
}

recent_file_records() {
  local root="$1"
  local pattern="$2"
  local maxdepth="$3"
  [[ -d "${root}" ]] || return 0
  find "${root}" -maxdepth "${maxdepth}" -name "${pattern}" -type f -print0 2>/dev/null \
    | xargs -0 stat -f '%m %N' 2>/dev/null || true
}

copy_crash_artifact() {
  local source_path="$1"
  [[ -f "${source_path}" ]] || return 0
  local hash
  local base
  hash="$(printf '%s' "${source_path}" | shasum -a 256 | awk '{print substr($1, 1, 12)}')"
  base="$(basename "${source_path}")"
  cp -p "${source_path}" "${CRASH_ARTIFACT_DIR}/${hash}-${base}" 2>/dev/null || true
}

record_new_crash_files() {
  local launch_epoch="$1"
  local failed=0
  local crash_threshold_epoch=$((launch_epoch - VERIFY_CRASH_DETECTION_SLOP_SECONDS))
  : > "${CRASH_REPORT_FILE}"

  local build_output_dir
  build_output_dir="$(dirname "${BUILD_OUTPUT}")"
  local mono_roots=(
    "${VERIFY_APP_PATH}"
    "${VERIFY_APP_PATH}/Contents"
    "${VERIFY_APP_PATH}/Contents/MacOS"
    "${build_output_dir}"
    "${PWD}"
    "${REPO_ROOT}"
    "/private/tmp"
    "/tmp"
  )
  local mono_root
  local mono_record
  for mono_root in "${mono_roots[@]}"; do
    while IFS= read -r mono_record; do
      [[ -n "${mono_record}" ]] || continue
      local mono_epoch="${mono_record%% *}"
      local mono_path="${mono_record#* }"
      if [[ "${mono_epoch}" =~ ^[0-9]+$ && "${mono_epoch}" -ge "${crash_threshold_epoch}" ]]; then
        if ! grep -Fqx "mono=${mono_path}" "${CRASH_REPORT_FILE}" 2>/dev/null; then
          echo "mono=${mono_path}" >> "${CRASH_REPORT_FILE}"
          copy_crash_artifact "${mono_path}"
        fi
        failed=1
      fi
    done < <(recent_file_records "${mono_root}" 'mono_crash*.json' 2)
  done

  local diagnostic_record
  while IFS= read -r diagnostic_record; do
    [[ -n "${diagnostic_record}" ]] || continue
    local diagnostic_epoch="${diagnostic_record%% *}"
    local diagnostic_path="${diagnostic_record#* }"
    if [[ "${diagnostic_epoch}" =~ ^[0-9]+$ && "${diagnostic_epoch}" -ge "${crash_threshold_epoch}" ]]; then
      if ! grep -Fqx "macos=${diagnostic_path}" "${CRASH_REPORT_FILE}" 2>/dev/null; then
        echo "macos=${diagnostic_path}" >> "${CRASH_REPORT_FILE}"
        copy_crash_artifact "${diagnostic_path}"
      fi
      failed=1
    fi
  done < <(
    {
      recent_file_records "${HOME}/Library/Logs/DiagnosticReports" 'TokenForge*.crash' 1
      recent_file_records "${HOME}/Library/Logs/DiagnosticReports" 'TokenForge*.ips' 1
    } | sort -nr
  )

  return "${failed}"
}

record_new_crash_files_with_wait() {
  local launch_epoch="$1"
  local waited=0

  while true; do
    if ! record_new_crash_files "${launch_epoch}"; then
      return 1
    fi

    if [[ "${waited}" -ge "${VERIFY_CRASH_REPORT_WAIT_SECONDS}" ]]; then
      return 0
    fi

    sleep 1
    waited=$((waited + 1))
  done
}

new_macos_crash_paths() {
  [[ -f "${CRASH_REPORT_FILE}" ]] || return 0
  awk -F= '$1 == "macos" {print substr($0, index($0, "=") + 1)}' "${CRASH_REPORT_FILE}"
}

new_mono_crash_paths() {
  [[ -f "${CRASH_REPORT_FILE}" ]] || return 0
  awk -F= '$1 == "mono" {print substr($0, index($0, "=") + 1)}' "${CRASH_REPORT_FILE}"
}

mono_crash_origin() {
  local crash_file="$1"
  local build_output_dir
  build_output_dir="$(dirname "${BUILD_OUTPUT}")"

  if [[ "${crash_file}" == "${VERIFY_APP_PATH}"/* ]] ||
     [[ "${crash_file}" == "${build_output_dir}"/* ]] ||
     [[ "${crash_file}" == "${BUILD_OUTPUT}"/* ]]; then
    echo "Player"
  elif [[ "${crash_file}" == *"Unity.app"* ]] ||
       [[ "${crash_file}" == *"/Unity/"* ]]; then
    echo "Editor"
  else
    echo "unknown"
  fi
}

mono_runtime_crash_detected() {
  local crash_file
  while IFS= read -r crash_file; do
    [[ -f "${crash_file}" ]] || continue
    echo "${crash_file}"
    return 0
  done < <(new_mono_crash_paths)

  return 1
}

launchservices_registration_abort_detected() {
  local crash_file
  while IFS= read -r crash_file; do
    [[ -f "${crash_file}" ]] || continue
    if LC_ALL=C grep -aFq "_RegisterApplication" "${crash_file}" &&
       LC_ALL=C grep -aFq "HIServices" "${crash_file}" &&
       ! LC_ALL=C grep -aFq "libDesktopCompanionOverlay.dylib" "${crash_file}"; then
      echo "${crash_file}"
      return 0
    fi
  done < <(new_macos_crash_paths)

  return 1
}

verify_app_still_running() {
  local executable
  executable="$(app_executable)"
  if [[ ! -f "${executable}" ]]; then
    echo "FAIL: verify executable missing (${executable})"
    return 1
  fi

  if command -v pgrep >/dev/null 2>&1 && pgrep -f "${executable}" >/dev/null 2>&1; then
    echo "PASS: app stayed open for ${VERIFY_RUNTIME_WAIT_SECONDS}s"
    return 0
  fi

  echo "FAIL: app was not running after ${VERIFY_RUNTIME_WAIT_SECONDS}s (${executable})"
  return 1
}

terminate_verify_app() {
  local executable
  executable="$(app_executable)"
  echo "INFO [RuntimeVerify] terminate_verify_app path=${VERIFY_APP_PATH}"
  /usr/bin/osascript -e 'tell application "TokenForge" to quit' >/dev/null 2>&1 || true
  sleep 2
  if command -v pgrep >/dev/null 2>&1 && [[ -f "${executable}" ]]; then
    local pids
    pids="$(pgrep -f "${executable}" 2>/dev/null || true)"
    if [[ -n "${pids}" ]]; then
      echo "INFO [RuntimeVerify] terminate lingering verify app pids=$(echo "${pids}" | tr '\n' ',' | sed 's/,$//')"
      while IFS= read -r pid; do
        [[ -n "${pid}" ]] || continue
        kill -TERM "${pid}" >/dev/null 2>&1 || true
      done <<< "${pids}"
    fi
  fi
}

cleanup() {
  terminate_verify_app
  if [[ -n "${DIRECT_LAUNCH_PID}" ]] && kill -0 "${DIRECT_LAUNCH_PID}" >/dev/null 2>&1; then
    kill "${DIRECT_LAUNCH_PID}" >/dev/null 2>&1 || true
    wait "${DIRECT_LAUNCH_PID}" >/dev/null 2>&1 || true
  fi
  if [[ -n "${LOG_STREAM_PID}" ]] && kill -0 "${LOG_STREAM_PID}" >/dev/null 2>&1; then
    kill "${LOG_STREAM_PID}" >/dev/null 2>&1 || true
    wait "${LOG_STREAM_PID}" >/dev/null 2>&1 || true
  fi
}

run_preflight() {
  local check_unity_lock="$1"
  section "Preflight"
  PREFLIGHT_STATUS=0
  CHECK_UNITY_LOCK="${check_unity_lock}" RUN_TOKENFORGE_OPEN_PROBE=false REQUIRE_APP_INTEGRITY=false APP_BUNDLE_PATH="${BUILD_OUTPUT}" "${SCRIPT_DIR}/preflight-runtime-environment.sh" || PREFLIGHT_STATUS=$?
  if [[ "${PREFLIGHT_STATUS}" -eq 2 ]]; then
    echo "Preflight found a Unity lock/process blocker."
    echo "Runtime PASS is NOT claimed."
    exit 3
  fi
  if [[ "${PREFLIGHT_STATUS}" -ne 0 ]]; then
    echo "Preflight warning: continuing because runtime verification can use the copied app and direct executable launch fallback (status=${PREFLIGHT_STATUS})."
  fi
}

require_build_output_exists() {
  local classification="$1"
  if [[ -d "${BUILD_OUTPUT}" ]]; then
    return 0
  fi

  echo "Build output is missing: ${BUILD_OUTPUT}" >&2
  echo "Classification: ${classification}" >&2
  echo "Runtime PASS is NOT claimed." >&2
  exit 5
}

print_build_artifact_hash_report() {
  section "Build Artifact Hash Report"
  : > "${HASH_REPORT}"
  if [[ -f "${BUILD_OUTPUT}/Contents/MacOS/TokenForge" ]]; then
    shasum -a 256 "${BUILD_OUTPUT}/Contents/MacOS/TokenForge" >> "${HASH_REPORT}"
  fi
  find "${BUILD_OUTPUT}/Contents" -type f \( -name '*.dll' -o -name '*.dylib' -o -name '*.bundle' \) -print0 2>/dev/null \
    | xargs -0 shasum -a 256 >> "${HASH_REPORT}" 2>/dev/null || true
  cat "${HASH_REPORT}"
}

run_artifact_freshness_gate() {
  section "Build Artifact Freshness Gate"
  BUILD_OUTPUT="${BUILD_OUTPUT}" APP_BUNDLE_PATH="${BUILD_OUTPUT}" "${SCRIPT_DIR}/verify-macos-build-artifacts.sh"
}

trap cleanup EXIT
trap 'cleanup; exit 130' INT
trap 'cleanup; exit 143' TERM

section "Clean Rebuild"
if [[ "${VERIFY_SKIP_REBUILD}" == "true" ]]; then
  echo "Clean rebuild skipped because VERIFY_SKIP_REBUILD=true; existing Player bundle must pass the artifact freshness gate before runtime launch."
  require_build_output_exists "ARTIFACT_MISSING"
  print_build_artifact_hash_report
  run_artifact_freshness_gate
  run_preflight false
else
  run_preflight true
  if ! BUILD_OUTPUT="${BUILD_OUTPUT}" CLEAN_BUILD=true "${SCRIPT_DIR}/build-macos-smoke.sh"; then
    echo "Clean rebuild failed or was blocked."
    echo "Classification: $(build_status_value RESULT || echo BUILD_FAILED)"
    echo "Runtime PASS is NOT claimed."
    exit 2
  fi
  require_build_output_exists "ARTIFACT_MISSING_AFTER_REBUILD"
  print_build_artifact_hash_report
  run_artifact_freshness_gate
fi

section "Prepare Verify App"
rm -rf "${VERIFY_APP_PATH}"
cp -R "${BUILD_OUTPUT}" "${VERIFY_APP_PATH}"
xattr -cr "${VERIFY_APP_PATH}" 2>/dev/null || true
if [[ "${VERIFY_RESIGN_APP}" == "true" ]]; then
  if ! APP_PATH="${VERIFY_APP_PATH}" TOKENFORGE_RELEASE_SIGN_MODE=adhoc "${SCRIPT_DIR}/sign-macos-app.sh"; then
    echo "ad-hoc signing failed for ${VERIFY_APP_PATH}."
    echo "Runtime PASS is NOT claimed."
    exit 8
  fi
else
  echo "Ad-hoc signing skipped because VERIFY_RESIGN_APP=false."
fi
if ! codesign --verify --deep --strict --verbose=4 "${VERIFY_APP_PATH}"; then
  echo "codesign strict verification failed for ${VERIFY_APP_PATH}."
  echo "Runtime PASS is NOT claimed."
  exit 8
fi

section "LaunchServices Registration"
LS_READY=0
if "${LSREGISTER}" -f "${VERIFY_APP_PATH}"; then
  LS_READY=1
else
  echo "LaunchServices registration failed for ${VERIFY_APP_PATH}."
  if [[ "${VERIFY_LAUNCH_MODE}" == "open" ]]; then
    echo "VERIFY_LAUNCH_MODE=open requested, so direct launch fallback is disabled."
    echo "Runtime PASS is NOT claimed."
    exit 9
  fi
  echo "Continuing with direct executable launch fallback."
fi

launch_direct() {
  local executable
  executable="$(app_executable)"
  if [[ ! -x "${executable}" ]]; then
    echo "Direct launch executable is missing or not executable: ${executable}"
    return 1
  fi

  (
    cd "$(dirname "${executable}")"
    TOKENFORGE_VERIFY_RUNTIME=1 "${executable}" -TokenForgeVerifyRuntime YES
  ) >> "${DIRECT_LAUNCH_LOG_FILE}" 2>&1 &
  DIRECT_LAUNCH_PID=$!
  echo "INFO [RuntimeVerify] direct launch pid=${DIRECT_LAUNCH_PID} executable=${executable}"
}

launch_open() {
  TOKENFORGE_VERIFY_RUNTIME=1 open "${VERIFY_APP_PATH}" --args -TokenForgeVerifyRuntime YES
}

launch_verify_app() {
  if [[ "${VERIFY_LAUNCH_MODE}" == "direct" ]]; then
    launch_direct
    return $?
  fi

  if [[ "${LS_READY}" -eq 1 ]]; then
    if launch_open; then
      echo "INFO [RuntimeVerify] launch method=open"
      return 0
    fi

    if [[ "${VERIFY_LAUNCH_MODE}" == "open" ]]; then
      return 1
    fi
    echo "LaunchServices open failed; using direct executable launch fallback."
  fi

  launch_direct
}

if [[ "${VERIFY_LAUNCH_MODE}" != "auto" && "${VERIFY_LAUNCH_MODE}" != "open" && "${VERIFY_LAUNCH_MODE}" != "direct" ]]; then
  echo "Invalid VERIFY_LAUNCH_MODE=${VERIFY_LAUNCH_MODE}; expected auto, open, or direct."
  echo "Runtime PASS is NOT claimed."
  exit 9
fi

section "Launch Runtime Verification"
: > "${LOG_STREAM_FILE}"
: > "${LOG_SHOW_FILE}"
: > "${DIRECT_LAUNCH_LOG_FILE}"
launch_epoch="$(date +%s)"
log stream --style compact --predicate 'process CONTAINS "TokenForge"' --level debug > "${LOG_STREAM_FILE}" 2>&1 &
LOG_STREAM_PID=$!

if ! launch_verify_app; then
  echo "Launch failed for ${VERIFY_APP_PATH}."
  echo "Runtime PASS is NOT claimed."
  exit 10
fi
sleep "${VERIFY_RUNTIME_WAIT_SECONDS}"
log show --style compact --predicate 'process CONTAINS "TokenForge"' --last 2m --debug > "${LOG_SHOW_FILE}" 2>&1 || true

section "Required Runtime Logs"
MISSING=0
verify_app_still_running || MISSING=1
if record_new_crash_files_with_wait "${launch_epoch}"; then
  printf 'PASS: no new TokenForge mono/macOS crash files detected after launch\n'
else
  printf 'FAIL: new crash files detected after launch; see %s\n' "${CRASH_REPORT_FILE}"
  if MONO_RUNTIME_CRASH_FILE="$(mono_runtime_crash_detected)"; then
    MONO_RUNTIME_CRASH_ORIGIN="$(mono_crash_origin "${MONO_RUNTIME_CRASH_FILE}")"
    printf 'FAIL: Mono runtime crash detected (%s)\n' "${MONO_RUNTIME_CRASH_FILE}"
    printf 'INFO: mono crash origin=%s; copied artifacts are under %s\n' "${MONO_RUNTIME_CRASH_ORIGIN}" "${CRASH_ARTIFACT_DIR}"
    cleanup
    section "Result"
    echo "Log stream file: ${LOG_STREAM_FILE}"
    echo "Log show file: ${LOG_SHOW_FILE}"
    echo "Direct launch file: ${DIRECT_LAUNCH_LOG_FILE}"
    echo "Hash report: ${HASH_REPORT}"
    echo "Crash report file: ${CRASH_REPORT_FILE}"
    echo "Crash artifact dir: ${CRASH_ARTIFACT_DIR}"
    echo "Classification: MONO_RUNTIME_CRASH"
    echo "Runtime PASS is NOT claimed."
    exit 11
  elif LAUNCHSERVICES_ABORT_CRASH="$(launchservices_registration_abort_detected)"; then
    printf 'BLOCKED: LaunchServices registration abort detected before managed startup (%s)\n' "${LAUNCHSERVICES_ABORT_CRASH}"
    printf 'INFO: crash signature is _RegisterApplication/HIServices with no libDesktopCompanionOverlay.dylib image; this verifier cannot claim runtime PASS in the current sandbox.\n'
    cleanup
    section "Result"
    echo "Log stream file: ${LOG_STREAM_FILE}"
    echo "Log show file: ${LOG_SHOW_FILE}"
    echo "Direct launch file: ${DIRECT_LAUNCH_LOG_FILE}"
    echo "Hash report: ${HASH_REPORT}"
    echo "Crash report file: ${CRASH_REPORT_FILE}"
    echo "Crash artifact dir: ${CRASH_ARTIFACT_DIR}"
    echo "Classification: LAUNCHSERVICES_REGISTRATION_ABORT"
    echo "Runtime PASS is NOT claimed."
    exit 8
  fi
  MISSING=1
fi
require_log "[BuildIdentity][RUNTIME_CODE_VERSION]" "runtime build identity marker" || MISSING=1
require_log "[ManagedStartup][ENTER]" "managed startup entered" || MISSING=1
require_log "[ManagedStartup][AFTER_BOOTSTRAP]" "managed startup after bootstrap" || MISSING=1
require_log "[StartupDiagnostic][IDLE_REACHED]" "startup reached idle" || MISSING=1
require_log "[RuntimeVerify][ENABLED]" "verification mode enabled" || MISSING=1
require_log "[CrashRecovery][SUPPRESSED_REPORT_UI] reason=verificationMode" "crash recovery report UI suppressed" || MISSING=1
require_log "reportIssueAutoPresent=false route=manualOnly" "Report Issue remains manual-only" || MISSING=1
require_log "[DashboardLifecycle][SUPPRESS_REOPEN] reason=verificationMode" "dashboard reopen suppressed" || MISSING=1
require_log "[OverlayWatchdog][SUPPRESSED] reason=verificationModeWarmup" "overlay watchdog suppressed during warmup" || MISSING=1
require_log "[WindowsDump][LAUNCH_STABLE] phase=launch+15s" "windows stable after launch" || MISSING=1
if log_contains "[QuitDiagnostic][PROCEED] request=applicationShouldTerminate source=appkit"; then
  echo "FAIL: implicit AppKit termination proceeded before verifier cleanup"
  MISSING=1
fi
cleanup

section "Result"
echo "Log stream file: ${LOG_STREAM_FILE}"
echo "Log show file: ${LOG_SHOW_FILE}"
echo "Direct launch file: ${DIRECT_LAUNCH_LOG_FILE}"
echo "Hash report: ${HASH_REPORT}"
echo "Crash report file: ${CRASH_REPORT_FILE}"
echo "Crash artifact dir: ${CRASH_ARTIFACT_DIR}"

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

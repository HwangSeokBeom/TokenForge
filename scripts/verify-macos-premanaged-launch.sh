#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

SOURCE_APP_PATH="${APP_PATH:-${BUILD_OUTPUT:-${REPO_ROOT}/UnityClient/builds/macOS/TokenForge.app}}"
VERIFY_APP_PATH="${VERIFY_APP_PATH:-/private/tmp/TokenForge-premanaged-verify.app}"
VERIFY_LOG_DIR="${VERIFY_LOG_DIR:-/private/tmp/tokenforge-premanaged-verify}"
WAIT_SECONDS="${VERIFY_PREMANAGED_WAIT_SECONDS:-60}"
OPEN_SETTLE_SECONDS="${VERIFY_PREMANAGED_OPEN_SETTLE_SECONDS:-8}"
PLAYER_LOG="${PLAYER_LOG:-${HOME}/Library/Logs/TokenForge/TokenForge/Player.log}"
RESIGN_VERIFY_APP="${RESIGN_VERIFY_APP:-true}"
VERIFY_RESET_LAUNCHSERVICES="${VERIFY_RESET_LAUNCHSERVICES:-false}"
PRE_RESET_CLASSIFICATION="${VERIFY_PRE_RESET_CLASSIFICATION:-}"
LSREGISTER="${LSREGISTER:-/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister}"

OPEN_LOG="${VERIFY_LOG_DIR}/open-launch.log"
DIRECT_LOG="${VERIFY_LOG_DIR}/direct-launch.log"
CRASH_REPORT="${VERIFY_LOG_DIR}/crash-files-after-launch.txt"
SUMMARY_FILE="${VERIFY_LOG_DIR}/classification.txt"

FAILED=0
CLASSIFICATION="UNKNOWN"
DIRECT_PID=""
PLAYER_LOG_BASELINE_EPOCH=0
RESET_RAN=false
RESET_STATUS="skipped"

section() {
  printf '\n== %s ==\n' "$1"
}

row() {
  printf '%-36s | %-10s | %s\n' "$1" "$2" "$3"
}

warn() {
  row "$1" "WARN" "$2"
}

info() {
  row "$1" "INFO" "$2"
}

ok() {
  row "$1" "OK" "$2"
}

fail() {
  row "$1" "FAIL" "$2"
  FAILED=1
}

plist_value() {
  local plist="$1"
  local key="$2"
  /usr/libexec/PlistBuddy -c "Print :${key}" "${plist}" 2>/dev/null || true
}

print_plist_key() {
  local plist="$1"
  local key="$2"
  local value
  value="$(plist_value "${plist}" "${key}")"
  if [[ -n "${value}" ]]; then
    row "${key}" "INFO" "${value}"
  else
    row "${key}" "MISSING" "-"
  fi
}

app_executable() {
  local app="$1"
  local executable_name
  executable_name="$(plist_value "${app}/Contents/Info.plist" CFBundleExecutable)"
  if [[ -z "${executable_name}" ]]; then
    executable_name="TokenForge"
  fi
  printf '%s/Contents/MacOS/%s' "${app}" "${executable_name}"
}

mtime_epoch() {
  stat -f '%m' "$1" 2>/dev/null || echo 0
}

new_crashes_since() {
  local since_epoch="$1"
  : > "${CRASH_REPORT}"
  find "${HOME}/Library/Logs/DiagnosticReports" -maxdepth 1 \( -name 'TokenForge*.ips' -o -name 'TokenForge*.crash' \) -print0 2>/dev/null \
    | while IFS= read -r -d '' crash_file; do
        local crash_epoch
        crash_epoch="$(mtime_epoch "${crash_file}")"
        if [[ "${crash_epoch}" =~ ^[0-9]+$ && "${crash_epoch}" -ge "${since_epoch}" ]]; then
          printf '%s\n' "${crash_file}" >> "${CRASH_REPORT}"
        fi
      done
  [[ -s "${CRASH_REPORT}" ]]
}

latest_new_crash() {
  if [[ ! -s "${CRASH_REPORT}" ]]; then
    return 1
  fi
  while IFS= read -r crash_file; do
    [[ -f "${crash_file}" ]] || continue
    printf '%s %s\n' "$(mtime_epoch "${crash_file}")" "${crash_file}"
  done < "${CRASH_REPORT}" | sort -nr | head -1 | sed 's/^[0-9]* //'
}

crash_contains() {
  local crash_file="$1"
  local pattern="$2"
  [[ -f "${crash_file}" ]] && LC_ALL=C grep -aFq "${pattern}" "${crash_file}"
}

player_log_is_fresh() {
  [[ -f "${PLAYER_LOG}" ]] || return 1
  local player_log_epoch
  player_log_epoch="$(mtime_epoch "${PLAYER_LOG}")"
  [[ "${player_log_epoch}" =~ ^[0-9]+$ && "${player_log_epoch}" -gt "${PLAYER_LOG_BASELINE_EPOCH}" ]]
}

runtime_log_contains_fixed() {
  local pattern="$1"
  if grep -Fq "${pattern}" "${DIRECT_LOG}" "${OPEN_LOG}" 2>/dev/null; then
    return 0
  fi
  if player_log_is_fresh && grep -Fq "${pattern}" "${PLAYER_LOG}" 2>/dev/null; then
    return 0
  fi
  return 1
}

runtime_log_contains_regex() {
  local pattern="$1"
  if grep -Eq "${pattern}" "${DIRECT_LOG}" "${OPEN_LOG}" 2>/dev/null; then
    return 0
  fi
  if player_log_is_fresh && grep -Eq "${pattern}" "${PLAYER_LOG}" 2>/dev/null; then
    return 0
  fi
  return 1
}

managed_markers_present() {
  runtime_log_contains_fixed "[BuildIdentity][RUNTIME_CODE_VERSION]" \
    && runtime_log_contains_fixed "[StartupDiagnostic][IDLE_REACHED]"
}

any_managed_start_present() {
  runtime_log_contains_regex '\[BuildIdentity\]|\[StartupDiagnostic\]|\[RuntimeVerify\]'
}

terminate_app() {
  local bundle_id
  bundle_id="$(plist_value "${VERIFY_APP_PATH}/Contents/Info.plist" CFBundleIdentifier)"
  if [[ -n "${bundle_id}" ]]; then
    /usr/bin/osascript -e "tell application id \"${bundle_id}\" to quit" >/dev/null 2>&1 || true
  fi
  if [[ -n "${DIRECT_PID}" ]] && kill -0 "${DIRECT_PID}" >/dev/null 2>&1; then
    kill -TERM "${DIRECT_PID}" >/dev/null 2>&1 || true
    wait "${DIRECT_PID}" >/dev/null 2>&1 || true
  fi
}

trap terminate_app EXIT

mkdir -p "${VERIFY_LOG_DIR}"
if [[ -z "${PRE_RESET_CLASSIFICATION}" && -f "${SUMMARY_FILE}" ]]; then
  PRE_RESET_CLASSIFICATION="$(tail -n 1 "${SUMMARY_FILE}" 2>/dev/null || true)"
fi
: > "${OPEN_LOG}"
: > "${DIRECT_LOG}"
: > "${SUMMARY_FILE}"

section "Bundle Paths"
info "source app" "${SOURCE_APP_PATH}"
info "verify app" "${VERIFY_APP_PATH}"
info "log dir" "${VERIFY_LOG_DIR}"
info "player log" "${PLAYER_LOG}"
info "reset LaunchServices" "${VERIFY_RESET_LAUNCHSERVICES}"
if [[ -n "${PRE_RESET_CLASSIFICATION}" ]]; then
  info "pre-reset classification" "${PRE_RESET_CLASSIFICATION}"
fi

if [[ ! -d "${SOURCE_APP_PATH}" ]]; then
  fail "source app exists" "missing"
  CLASSIFICATION="SIGNING_OR_BUNDLE_INVALID"
  echo "${CLASSIFICATION}" | tee "${SUMMARY_FILE}"
  exit 2
fi

section "LaunchServices Reset"
if [[ "${VERIFY_RESET_LAUNCHSERVICES}" == "true" ]]; then
  if [[ -x "${LSREGISTER}" ]]; then
    if "${LSREGISTER}" -kill -r -domain local -domain system -domain user; then
      ok "lsregister reset" "completed"
      RESET_RAN=true
      RESET_STATUS="completed"
    else
      warn "lsregister reset" "exact requested command failed"
      RESET_STATUS="requested_command_failed"
      if "${LSREGISTER}" -r -domain local -domain system -domain user; then
        warn "lsregister fallback" "completed without deprecated -kill"
        RESET_STATUS="fallback_without_kill_completed"
      else
        warn "lsregister fallback" "failed without deprecated -kill"
      fi
    fi
  else
    warn "lsregister reset" "not found at ${LSREGISTER}"
    RESET_STATUS="lsregister_missing"
  fi

  if killall lsd sharedfilelistd 2>/dev/null; then
    ok "LaunchServices daemons" "restart requested"
  else
    warn "LaunchServices daemons" "no matching daemon was killed or killall failed"
  fi
else
  info "lsregister reset" "skipped; set VERIFY_RESET_LAUNCHSERVICES=true to enable"
fi

section "Prepare Clean Verify Bundle"
rm -rf "${VERIFY_APP_PATH}"
cp -R "${SOURCE_APP_PATH}" "${VERIFY_APP_PATH}"
xattr -cr "${VERIFY_APP_PATH}" 2>/dev/null || true
ok "copied app" "${VERIFY_APP_PATH}"

section "Info.plist Identity"
INFO_PLIST="${VERIFY_APP_PATH}/Contents/Info.plist"
if [[ ! -f "${INFO_PLIST}" ]]; then
  fail "Info.plist" "missing"
else
  for key in \
    CFBundleIdentifier \
    CFBundleExecutable \
    CFBundlePackageType \
    CFBundleName \
    CFBundleDisplayName \
    CFBundleShortVersionString \
    CFBundleVersion \
    LSMinimumSystemVersion \
    NSHighResolutionCapable \
    NSPrincipalClass \
    NSMainNibFile \
    LSUIElement \
    LSBackgroundOnly \
    UIApplicationSceneManifest; do
    print_plist_key "${INFO_PLIST}" "${key}"
  done
fi

section "Executable"
EXECUTABLE_PATH="$(app_executable "${VERIFY_APP_PATH}")"
info "executable path" "${EXECUTABLE_PATH}"
if [[ -x "${EXECUTABLE_PATH}" ]]; then
  ok "executable permissions" "$(ls -l "${EXECUTABLE_PATH}")"
else
  fail "executable permissions" "missing or not executable"
fi

section "Extended Attributes"
if xattr -p com.apple.quarantine "${VERIFY_APP_PATH}" >/dev/null 2>&1; then
  fail "quarantine" "present"
else
  ok "quarantine" "absent"
fi
remaining_xattrs="$(xattr -lr "${VERIFY_APP_PATH}" 2>/dev/null || true)"
if [[ -n "${remaining_xattrs}" ]]; then
  warn "remaining xattrs" "present; first entries follow"
  printf '%s\n' "${remaining_xattrs}" | sed -n '1,40p'
else
  ok "remaining xattrs" "none"
fi

section "Code Signing"
if [[ "${RESIGN_VERIFY_APP}" == "true" ]]; then
  if APP_PATH="${VERIFY_APP_PATH}" TOKENFORGE_RELEASE_SIGN_MODE="${TOKENFORGE_RELEASE_SIGN_MODE:-adhoc}" "${SCRIPT_DIR}/sign-macos-app.sh"; then
    ok "ad-hoc re-sign" "completed"
  else
    fail "ad-hoc re-sign" "failed"
    CLASSIFICATION="SIGNING_OR_BUNDLE_INVALID"
  fi
fi

if [[ "${CLASSIFICATION}" != "SIGNING_OR_BUNDLE_INVALID" ]]; then
  if codesign --verify --deep --strict --verbose=4 "${VERIFY_APP_PATH}"; then
    ok "codesign verify" "valid"
  else
    fail "codesign verify" "invalid"
    CLASSIFICATION="SIGNING_OR_BUNDLE_INVALID"
  fi
fi

if command -v spctl >/dev/null 2>&1; then
  if spctl --assess --type execute --verbose=4 "${VERIFY_APP_PATH}"; then
    ok "spctl" "accepted"
  else
    warn "spctl" "not accepted; local ad-hoc dev builds may be rejected"
  fi
else
  warn "spctl" "not available"
fi

section "Linkage"
if [[ -f "${EXECUTABLE_PATH}" ]]; then
  otool -L "${EXECUTABLE_PATH}" || true
  otool -l "${EXECUTABLE_PATH}" | awk '
    /cmd LC_RPATH/ {show=1}
    show {print}
    show && /path / {show=0}
  ' || true
fi
UNITY_PLAYER="${VERIFY_APP_PATH}/Contents/Frameworks/UnityPlayer.dylib"
if [[ -f "${UNITY_PLAYER}" ]]; then
  otool -L "${UNITY_PLAYER}" || true
  otool -l "${UNITY_PLAYER}" | awk '
    /cmd LC_RPATH/ {show=1}
    show {print}
    show && /path / {show=0}
  ' || true
else
  fail "UnityPlayer.dylib" "missing"
fi

if [[ "${CLASSIFICATION}" == "SIGNING_OR_BUNDLE_INVALID" ]]; then
  section "Classification"
  echo "${CLASSIFICATION}" | tee "${SUMMARY_FILE}"
  exit 3
fi

section "LaunchServices Open Probe"
launch_epoch="$(date +%s)"
PLAYER_LOG_BASELINE_EPOCH="$(mtime_epoch "${PLAYER_LOG}")"
if [[ -x "${LSREGISTER}" ]]; then
  if "${LSREGISTER}" -f "${VERIFY_APP_PATH}" >> "${OPEN_LOG}" 2>&1; then
    ok "lsregister -f" "completed"
  else
    warn "lsregister -f" "failed; see ${OPEN_LOG}"
  fi
else
  warn "lsregister" "not found at ${LSREGISTER}"
fi

if open -n "${VERIFY_APP_PATH}" --args -TokenForgeVerifyRuntime YES >> "${OPEN_LOG}" 2>&1; then
  ok "open launch" "command returned success"
else
  warn "open launch" "command failed; see ${OPEN_LOG}"
fi
sleep "${OPEN_SETTLE_SECONDS}"
terminate_app

if grep -Eq 'RegisterApplication|kLSNoExecutableErr|LSOpen|HIServices|failed to scan' "${OPEN_LOG}" 2>/dev/null; then
  warn "LaunchServices open probe" "LaunchServices error text observed"
fi

section "Direct Executable 60s Probe"
: > "${DIRECT_LOG}"
direct_epoch="$(date +%s)"
(
  cd "$(dirname "${EXECUTABLE_PATH}")"
  TOKENFORGE_VERIFY_RUNTIME=1 "./$(basename "${EXECUTABLE_PATH}")" -TokenForgeVerifyRuntime YES
) >> "${DIRECT_LOG}" 2>&1 &
DIRECT_PID=$!
info "direct pid" "${DIRECT_PID}"
sleep "${WAIT_SECONDS}"

RUNNING_AFTER_WAIT=false
if kill -0 "${DIRECT_PID}" >/dev/null 2>&1; then
  RUNNING_AFTER_WAIT=true
  ok "process state" "running after ${WAIT_SECONDS}s"
else
  wait "${DIRECT_PID}" >/dev/null 2>&1 || true
  fail "process state" "exited before ${WAIT_SECONDS}s"
fi

if new_crashes_since "${launch_epoch}"; then
  warn "new crash files" "detected; see ${CRASH_REPORT}"
  cat "${CRASH_REPORT}"
else
  ok "new crash files" "none"
fi

LATEST_CRASH="$(latest_new_crash || true)"
if [[ -n "${LATEST_CRASH}" ]]; then
  info "latest crash" "${LATEST_CRASH}"
  if crash_contains "${LATEST_CRASH}" "_RegisterApplication"; then
    warn "crash frame" "_RegisterApplication present"
    CLASSIFICATION="LAUNCHSERVICES_REGISTRATION_ABORT"
  fi
  if crash_contains "${LATEST_CRASH}" "libDesktopCompanionOverlay.dylib"; then
    warn "overlay image" "libDesktopCompanionOverlay.dylib appeared in crash image list"
  else
    ok "overlay image" "libDesktopCompanionOverlay.dylib absent from crash image list"
  fi
fi

section "Managed Startup Markers"
if managed_markers_present; then
  ok "managed markers" "BuildIdentity and idle markers present"
else
  fail "managed markers" "BuildIdentity and idle markers missing"
fi

if any_managed_start_present; then
  ok "managed startup" "some managed/runtime marker appeared"
else
  warn "managed startup" "no managed/runtime marker appeared"
fi

section "Classification"
if [[ "${RUNNING_AFTER_WAIT}" == "true" && "${FAILED}" -eq 0 && -z "${LATEST_CRASH}" ]]; then
  CLASSIFICATION="RUNNING_AFTER_60S"
elif [[ "${RUNNING_AFTER_WAIT}" == "true" && "$(managed_markers_present && echo yes || echo no)" == "yes" ]]; then
  CLASSIFICATION="MANAGED_STARTED"
elif [[ "${CLASSIFICATION}" == "LAUNCHSERVICES_REGISTRATION_ABORT" ]]; then
  :
elif [[ -n "${LATEST_CRASH}" || "${RUNNING_AFTER_WAIT}" != "true" ]]; then
  CLASSIFICATION="PRE_MANAGED_CRASH"
fi

echo "${CLASSIFICATION}" | tee "${SUMMARY_FILE}"
if [[ "${RESET_RAN}" == "true" || -n "${PRE_RESET_CLASSIFICATION}" ]]; then
  echo "Pre-reset classification: ${PRE_RESET_CLASSIFICATION:-UNKNOWN}"
  echo "Post-reset classification: ${CLASSIFICATION}"
  echo "LaunchServices reset status: ${RESET_STATUS}"
fi
echo "Summary file: ${SUMMARY_FILE}"
echo "Open log: ${OPEN_LOG}"
echo "Direct log: ${DIRECT_LOG}"
echo "Crash report: ${CRASH_REPORT}"

if [[ "${CLASSIFICATION}" == "RUNNING_AFTER_60S" && "$(managed_markers_present && echo yes || echo no)" == "yes" ]]; then
  exit 0
fi

exit 4

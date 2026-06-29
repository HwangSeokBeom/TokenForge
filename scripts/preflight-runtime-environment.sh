#!/usr/bin/env bash
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
LOCK_FILE="${UNITY_PROJECT_PATH}/Temp/UnityLockfile"
APP_BUNDLE_PATH="${APP_BUNDLE_PATH:-/private/tmp/TokenForge-verify.app}"
TEXTEDIT_APP="${TEXTEDIT_APP:-/System/Applications/TextEdit.app}"
CALCULATOR_APP="${CALCULATOR_APP:-/System/Applications/Calculator.app}"
LSREGISTER="${LSREGISTER:-/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister}"
RUN_OPEN_PROBES="${RUN_OPEN_PROBES:-true}"
RUN_TOKENFORGE_OPEN_PROBE="${RUN_TOKENFORGE_OPEN_PROBE:-false}"
REQUIRE_APP_INTEGRITY="${REQUIRE_APP_INTEGRITY:-true}"
CHECK_UNITY_LOCK="${CHECK_UNITY_LOCK:-true}"

LOCK_BLOCKED=0
LS_UNAVAILABLE=0
APP_INTEGRITY_FAILED=0

section() {
  printf '\n== %s ==\n' "$1"
}

row() {
  printf '%-34s | %-10s | %s\n' "$1" "$2" "$3"
}

run_capture() {
  local label="$1"
  shift
  local output
  local status

  output="$("$@" 2>&1)"
  status=$?
  if [[ ${status} -eq 0 ]]; then
    row "${label}" "OK" "${output:-exit 0}"
  else
    row "${label}" "FAIL" "exit ${status}: ${output:-no output}"
  fi
  return "${status}"
}

plist_value() {
  local plist="$1"
  local key="$2"
  if [[ -x /usr/libexec/PlistBuddy ]]; then
    /usr/libexec/PlistBuddy -c "Print :${key}" "${plist}" 2>/dev/null
  else
    defaults read "${plist}" "${key}" 2>/dev/null
  fi
}

section "Session"
row "repo root" "INFO" "${REPO_ROOT}"
row "user" "INFO" "$(id -un 2>/dev/null || echo unknown)"
row "uid" "INFO" "$(id -u 2>/dev/null || echo unknown)"
row "shell" "INFO" "${SHELL:-unknown}"
row "date" "INFO" "$(date '+%Y-%m-%d %H:%M:%S %Z' 2>/dev/null || date)"

section "Unity Lock"
if [[ "${CHECK_UNITY_LOCK}" != "true" ]]; then
  row "UnityLockfile exists" "SKIPPED" "CHECK_UNITY_LOCK=false"
elif [[ -f "${LOCK_FILE}" ]]; then
  LOCK_BLOCKED=1
  row "UnityLockfile exists" "BLOCKED" "${LOCK_FILE}"
  if command -v lsof >/dev/null 2>&1; then
    LSOF_OUTPUT="$(lsof "${LOCK_FILE}" 2>&1)"
    LSOF_STATUS=$?
    if [[ ${LSOF_STATUS} -eq 0 ]]; then
      printf '%s\n' "${LSOF_OUTPUT}" | sed 's/^/  /'
      row "lsof holder" "INFO" "listed above"
      if printf '%s\n' "${LSOF_OUTPUT}" | grep -qi '^Unity'; then
        row "Unity lock holder" "BLOCKED" "Unity process holds UnityLockfile"
      fi
      if printf '%s\n' "${LSOF_OUTPUT}" | grep -Eqi '^fileprovi|^fileproviderd'; then
        row "fileproviderd lock holder" "BLOCKED" "project may be inside iCloud/File Provider/Dropbox/OneDrive sync storage"
      fi
    else
      row "lsof UnityLockfile" "UNKNOWN" "exit ${LSOF_STATUS}: ${LSOF_OUTPUT}"
    fi
  else
    row "lsof UnityLockfile" "UNKNOWN" "lsof is not available"
  fi
else
  row "UnityLockfile exists" "OK" "not present"
fi

section "Unity Processes"
if [[ "${CHECK_UNITY_LOCK}" != "true" ]]; then
  row "Unity-related processes" "SKIPPED" "CHECK_UNITY_LOCK=false"
elif command -v pgrep >/dev/null 2>&1; then
  PGREP_OUTPUT="$(pgrep -fl "Unity|Unity Hub|Unity Package Manager|UPM|Unity Licensing|LicensingClient|AssetImportWorker" 2>&1)"
  PGREP_STATUS=$?
  if [[ ${PGREP_STATUS} -eq 0 ]]; then
    LOCK_BLOCKED=1
    row "Unity/UPM/Licensing/AssetImportWorker" "BLOCKED" "visible processes remain"
    printf '%s\n' "${PGREP_OUTPUT}" | sed 's/^/  /'
  else
    row "Unity/UPM/Licensing/AssetImportWorker" "OK" "none visible, or process listing returned no matches"
  fi
  FILEPROVIDER_OUTPUT="$(pgrep -fl "fileproviderd|FileProvider" 2>&1)"
  FILEPROVIDER_STATUS=$?
  if [[ ${FILEPROVIDER_STATUS} -eq 0 ]]; then
    row "fileproviderd possibility" "INFO" "File Provider sync process visible; avoid synced project paths for Unity rebuilds"
    printf '%s\n' "${FILEPROVIDER_OUTPUT}" | sed 's/^/  /'
  else
    row "fileproviderd possibility" "OK" "no fileproviderd/FileProvider process visible"
  fi
else
  row "Unity-related processes" "UNKNOWN" "pgrep is not available"
fi
row "manual quit/kill performed" "NO" "script is read-only; use Activity Monitor or normal Terminal if needed"
row "safe manual delete" "INFO" "quit Unity Editor/Hub/UPM/Licensing/AssetImportWorker first; if lsof shows no holder, remove ${LOCK_FILE} manually"

section "LaunchServices System Comparison"
if [[ -x "${LSREGISTER}" ]]; then
  run_capture "lsregister TextEdit" "${LSREGISTER}" -f "${TEXTEDIT_APP}"
  LSREG_TEXTEDIT=$?
  run_capture "lsregister Calculator" "${LSREGISTER}" -f "${CALCULATOR_APP}"
  LSREG_CALCULATOR=$?
  if [[ ${LSREG_TEXTEDIT} -ne 0 && ${LSREG_CALCULATOR} -ne 0 ]]; then
    LS_UNAVAILABLE=1
  fi
else
  LS_UNAVAILABLE=1
  row "lsregister executable" "FAIL" "${LSREGISTER} is not executable"
fi

if [[ "${RUN_OPEN_PROBES}" == "true" ]]; then
  run_capture "open TextEdit" open -gj "${TEXTEDIT_APP}"
  OPEN_TEXTEDIT=$?
  run_capture "open Calculator" open -gj "${CALCULATOR_APP}"
  OPEN_CALCULATOR=$?
  if [[ ${OPEN_TEXTEDIT} -ne 0 && ${OPEN_CALCULATOR} -ne 0 ]]; then
    LS_UNAVAILABLE=1
  fi
else
  row "open system apps" "SKIPPED" "RUN_OPEN_PROBES=false"
fi

section "TokenForge App Bundle"
if [[ -d "${APP_BUNDLE_PATH}" ]]; then
  row "app bundle exists" "OK" "${APP_BUNDLE_PATH}"
  INFO_PLIST="${APP_BUNDLE_PATH}/Contents/Info.plist"
  if [[ -f "${INFO_PLIST}" ]]; then
    CF_BUNDLE_EXECUTABLE="$(plist_value "${INFO_PLIST}" CFBundleExecutable || true)"
    CF_BUNDLE_IDENTIFIER="$(plist_value "${INFO_PLIST}" CFBundleIdentifier || true)"
    if [[ -n "${CF_BUNDLE_EXECUTABLE}" ]]; then
      row "CFBundleExecutable" "OK" "${CF_BUNDLE_EXECUTABLE}"
    else
      APP_INTEGRITY_FAILED=1
      row "CFBundleExecutable" "FAIL" "missing"
    fi
    if [[ -n "${CF_BUNDLE_IDENTIFIER}" ]]; then
      row "CFBundleIdentifier" "OK" "${CF_BUNDLE_IDENTIFIER}"
    else
      row "CFBundleIdentifier" "UNKNOWN" "missing"
    fi
  else
    APP_INTEGRITY_FAILED=1
    row "Info.plist" "FAIL" "missing"
  fi

  EXECUTABLE_PATH="${APP_BUNDLE_PATH}/Contents/MacOS/${CF_BUNDLE_EXECUTABLE:-TokenForge}"
  if [[ -f "${EXECUTABLE_PATH}" ]]; then
    row "Contents/MacOS executable" "OK" "${EXECUTABLE_PATH}"
    if [[ -x "${EXECUTABLE_PATH}" ]]; then
      row "executable bit" "OK" "set"
    else
      APP_INTEGRITY_FAILED=1
      row "executable bit" "FAIL" "not set"
    fi
  else
    APP_INTEGRITY_FAILED=1
    row "Contents/MacOS executable" "FAIL" "${EXECUTABLE_PATH} missing"
  fi

  run_capture "codesign strict verify" codesign --verify --deep --strict --verbose=4 "${APP_BUNDLE_PATH}"
  CODESIGN_STATUS=$?
  if [[ ${CODESIGN_STATUS} -ne 0 ]]; then
    APP_INTEGRITY_FAILED=1
  fi

  if command -v xattr >/dev/null 2>&1; then
    XATTR_OUTPUT="$(xattr -lr "${APP_BUNDLE_PATH}" 2>&1)"
    XATTR_STATUS=$?
    if [[ ${XATTR_STATUS} -eq 0 ]]; then
      if [[ -n "${XATTR_OUTPUT}" ]]; then
        row "xattr report" "INFO" "extended attributes present"
        printf '%s\n' "${XATTR_OUTPUT}" | sed 's/^/  /'
      else
        row "xattr report" "OK" "no extended attributes"
      fi
    else
      row "xattr report" "UNKNOWN" "exit ${XATTR_STATUS}: ${XATTR_OUTPUT}"
    fi
  else
    row "xattr report" "UNKNOWN" "xattr is not available"
  fi

  if [[ -x "${LSREGISTER}" ]]; then
    run_capture "lsregister TokenForge" "${LSREGISTER}" -f "${APP_BUNDLE_PATH}"
    LSREG_TOKENFORGE=$?
    if [[ ${LSREG_TOKENFORGE} -ne 0 ]]; then
      LS_UNAVAILABLE=1
    fi
  fi

  if [[ "${RUN_TOKENFORGE_OPEN_PROBE}" == "true" ]]; then
    run_capture "open TokenForge" open -gj "${APP_BUNDLE_PATH}"
    OPEN_TOKENFORGE=$?
    if [[ ${OPEN_TOKENFORGE} -ne 0 ]]; then
      LS_UNAVAILABLE=1
    fi
  else
    row "open TokenForge" "SKIPPED" "RUN_TOKENFORGE_OPEN_PROBE=false"
  fi
else
  row "app bundle exists" "MISSING" "${APP_BUNDLE_PATH}"
fi

section "Readiness"
if [[ ${LOCK_BLOCKED} -eq 0 ]]; then
  row "clean rebuild allowed" "YES" "no Unity lock/process blocker detected by this script"
else
  row "clean rebuild allowed" "NO" "Unity lock or Unity-related process is still visible"
fi

if [[ ${LS_UNAVAILABLE} -eq 0 ]]; then
  row "LaunchServices available" "YES" "system comparison did not show environment-wide failure"
else
  row "LaunchServices available" "NO" "system app and/or TokenForge registration/open failed in this session"
fi

if [[ ${APP_INTEGRITY_FAILED} -eq 0 ]]; then
  row "app bundle integrity" "OK" "no app bundle integrity failure detected"
else
  row "app bundle integrity" "FAIL" "bundle metadata, executable, or codesign check failed"
fi

if [[ ${LOCK_BLOCKED} -ne 0 ]]; then
  echo "Preflight result: BLOCKED by Unity lock/process state."
  exit 2
fi

if [[ ${LS_UNAVAILABLE} -ne 0 ]]; then
  echo "Preflight result: BLOCKED by LaunchServices unavailability in this session."
  exit 3
fi

if [[ ${APP_INTEGRITY_FAILED} -ne 0 && "${REQUIRE_APP_INTEGRITY}" == "true" ]]; then
  echo "Preflight result: FAIL due to app bundle integrity issue."
  exit 4
fi

echo "Preflight result: PASS for environment readiness only. Runtime PASS is NOT claimed."

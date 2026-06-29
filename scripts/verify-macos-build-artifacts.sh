#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
BUILD_OUTPUT="${BUILD_OUTPUT:-/private/tmp/tokenforge-macos-build/TokenForge.app}"
APP_BUNDLE_PATH="${APP_BUNDLE_PATH:-${BUILD_OUTPUT}}"
LOG_FILE="${LOG_FILE:-/private/tmp/tokenforge-macos-build.log}"
SOURCE_DYLIB="${SOURCE_DYLIB:-${UNITY_PROJECT_PATH}/Assets/Plugins/macOS/libDesktopCompanionOverlay.dylib}"
BUNDLED_DYLIB="${BUNDLED_DYLIB:-${APP_BUNDLE_PATH}/Contents/PlugIns/libDesktopCompanionOverlay.dylib}"
PROJECT_MANAGED_DLL="${PROJECT_MANAGED_DLL:-${UNITY_PROJECT_PATH}/Library/ScriptAssemblies/TokenForge.Client.dll}"
BUNDLED_MANAGED_DLL="${BUNDLED_MANAGED_DLL:-${APP_BUNDLE_PATH}/Contents/Resources/Data/Managed/TokenForge.Client.dll}"
SOURCE_CS_ROOT="${SOURCE_CS_ROOT:-${UNITY_PROJECT_PATH}/Assets/_Project/Scripts}"
BUILD_OUTPUT_DIR="$(dirname "${BUILD_OUTPUT}")"
BUILD_STATUS_FILE="${BUILD_STATUS_FILE:-${BUILD_OUTPUT_DIR}/tokenforge-build-status.env}"
RUNTIME_MARKER="${RUNTIME_MARKER:-tokenforge_runtime_fix_20260613_mono_crash}"
LEGACY_RUNTIME_MARKER="${LEGACY_RUNTIME_MARKER:-app-bootstrapper-overlay-projection-v9}"

FAILED=0
APP_BUNDLE_CLASSIFICATION="UNKNOWN"

section() {
  printf '\n== %s ==\n' "$1"
}

row() {
  printf '%-32s | %-8s | %s\n' "$1" "$2" "$3"
}

fail() {
  row "$1" "FAIL" "$2"
  FAILED=1
}

ok() {
  row "$1" "OK" "$2"
}

info() {
  row "$1" "INFO" "$2"
}

warn() {
  row "$1" "WARN" "$2"
}

status_value() {
  local key="$1"
  [[ -f "${BUILD_STATUS_FILE}" ]] || return 1
  awk -F= -v key="${key}" '$1 == key {print substr($0, index($0, "=") + 1); exit}' "${BUILD_STATUS_FILE}"
}

log_contains() {
  local pattern="$1"
  [[ -f "${LOG_FILE}" ]] || return 1
  LC_ALL=C grep -aEq "${pattern}" "${LOG_FILE}"
}

classify_missing_app_bundle() {
  local result
  local previous_app_present
  result="$(status_value RESULT || true)"
  previous_app_present="$(status_value PREVIOUS_APP_PRESENT || true)"

  case "${result}" in
    ARTIFACT_MISSING_AFTER_BLOCKED_BUILD)
      echo "ARTIFACT_MISSING_AFTER_BLOCKED_BUILD"
      return 0
      ;;
    UPM_BLOCKED)
      echo "UPM_BLOCKED_BEFORE_BUILD"
      return 0
      ;;
    UNITY_LOCK_BLOCKED)
      echo "UNITY_LOCK_BLOCKED_BEFORE_BUILD"
      return 0
      ;;
  esac

  if [[ "${previous_app_present}" == "true" && "${result}" == "BUILD_FAILED" ]]; then
    echo "DELETED_BY_FAILED_CLEAN_REBUILD"
    return 0
  fi

  if log_contains 'CLEAN_OUTPUT.*removeApp=.*TokenForge.*\.app' &&
     ! log_contains 'ATOMIC_REPLACE|canonical_app_replaced_from_staging|Result: success'; then
    echo "DELETED_BY_FAILED_CLEAN_REBUILD"
    return 0
  fi

  if log_contains '(Unity Package Manager|UPM|Unity-Upm|/tmp/Unity-Upm-[^[:space:]]+\.sock).*(EPERM|Operation not permitted|listen)|((EPERM|Operation not permitted).*(Unity Package Manager|UPM|Unity-Upm|/tmp/Unity-Upm-[^[:space:]]+\.sock))'; then
    echo "UPM_BLOCKED_BEFORE_BUILD"
    return 0
  fi

  if [[ ! -d "${BUILD_OUTPUT_DIR}" && ! -f "${BUILD_STATUS_FILE}" && ! -f "${LOG_FILE}" ]]; then
    echo "NEVER_BUILT"
    return 0
  fi

  if [[ -z "${result}" ]]; then
    echo "NEVER_BUILT"
  else
    echo "ARTIFACT_MISSING_AFTER_${result}"
  fi
}

sha256_for_file() {
  shasum -a 256 "$1" | awk '{print $1}'
}

mtime_epoch() {
  stat -f '%m' "$1"
}

mtime_display() {
  date -r "$(mtime_epoch "$1")" -u '+%Y-%m-%dT%H:%M:%SZ'
}

latest_source_record() {
  find "${SOURCE_CS_ROOT}" -type f -name '*.cs' -print0 \
    | xargs -0 stat -f '%m %N' \
    | sort -nr \
    | head -1
}

contains_marker() {
  local path="$1"
  local marker="$2"
  local monodis_path
  local temp_il
  local mono_path

  if LC_ALL=C grep -aFq "${marker}" "${path}" 2>/dev/null; then
    echo "raw-bytes"
    return 0
  fi

  if strings "${path}" 2>/dev/null | grep -Fq "${marker}"; then
    echo "strings"
    return 0
  fi

  monodis_path="$(command -v monodis || true)"
  if [[ -n "${monodis_path}" ]]; then
    temp_il="$(mktemp "${TMPDIR:-/tmp}/tokenforge-managed-marker.XXXXXX")"
    mono_path="${UNITY_MANAGED_PATH}:${UNITY_MANAGED_PATH}/UnityEngine"
    if MONO_PATH="${mono_path}" "${monodis_path}" --output="${temp_il}" "${path}" >/dev/null 2>&1; then
      if grep -Fq "${marker}" "${temp_il}"; then
        rm -f "${temp_il}"
        echo "monodis-il"
        return 0
      fi
    fi
    rm -f "${temp_il}"
  fi

  return 1
}

source_contains_marker() {
  local root="$1"
  local marker="$2"

  grep -R -Fq "${marker}" "${root}" --include='*.cs'
}

managed_marker_candidates() {
  local path="$1"
  local monodis_path
  local temp_il
  local mono_path

  monodis_path="$(command -v monodis || true)"
  if [[ -z "${monodis_path}" ]]; then
    return 0
  fi

  temp_il="$(mktemp "${TMPDIR:-/tmp}/tokenforge-managed-marker-candidates.XXXXXX")"
  mono_path="${UNITY_MANAGED_PATH}:${UNITY_MANAGED_PATH}/UnityEngine"
  if MONO_PATH="${mono_path}" "${monodis_path}" --output="${temp_il}" "${path}" >/dev/null 2>&1; then
    grep -Eo 'tokenforge_runtime_fix_[0-9]{8}_[0-9]{6}|app-bootstrapper-overlay-projection-v[0-9]+' "${temp_il}" | sort -u | tr '\n' ' '
  fi
  rm -f "${temp_il}"
}

section "Build Paths"
info "repo root" "${REPO_ROOT}"
info "unity project path" "${UNITY_PROJECT_PATH}"
info "build output path" "${BUILD_OUTPUT}"
info "app bundle path" "${APP_BUNDLE_PATH}"
info "build status path" "${BUILD_STATUS_FILE}"
if [[ -f "${BUILD_STATUS_FILE}" ]]; then
  info "last build status" "$(status_value RESULT || echo unknown)"
fi

section "App Bundle"
if [[ ! -d "${APP_BUNDLE_PATH}" ]]; then
  APP_BUNDLE_CLASSIFICATION="$(classify_missing_app_bundle)"
  fail "app bundle exists" "classification=${APP_BUNDLE_CLASSIFICATION}; no app bundle at ${APP_BUNDLE_PATH}"
else
  ok "app bundle exists" "${APP_BUNDLE_PATH}"
  APP_BUNDLE_CLASSIFICATION="PRESENT"
fi

if [[ -d "${BUILD_OUTPUT_DIR}" ]]; then
  APP_BUNDLE_COUNT="$(find "${BUILD_OUTPUT_DIR}" -maxdepth 1 -type d -name 'TokenForge*.app' -print 2>/dev/null | wc -l | tr -d ' ')"
  if [[ "${APP_BUNDLE_COUNT}" == "1" ]]; then
    ok "single app bundle" "$(find "${BUILD_OUTPUT_DIR}" -maxdepth 1 -type d -name 'TokenForge*.app' -print 2>/dev/null)"
  else
    fail "single app bundle" "expected one TokenForge*.app in ${BUILD_OUTPUT_DIR}, found ${APP_BUNDLE_COUNT}: $(find "${BUILD_OUTPUT_DIR}" -maxdepth 1 -type d -name 'TokenForge*.app' -print 2>/dev/null | tr '\n' ' ')"
  fi

  MONO_CRASH_COUNT="$(find "${BUILD_OUTPUT_DIR}" -maxdepth 1 -type f -name 'mono_crash*.json' -print 2>/dev/null | wc -l | tr -d ' ')"
  if [[ "${MONO_CRASH_COUNT}" == "0" ]]; then
    ok "build dir mono crash" "none"
  else
    fail "build dir mono crash" "found stale/runtime mono crash files: $(find "${BUILD_OUTPUT_DIR}" -maxdepth 1 -type f -name 'mono_crash*.json' -print 2>/dev/null | tr '\n' ' ')"
  fi
else
  warn "build output directory" "missing: ${BUILD_OUTPUT_DIR}"
fi

section "Native Dylib"
if [[ ! -f "${SOURCE_DYLIB}" ]]; then
  fail "source dylib" "missing: ${SOURCE_DYLIB}"
else
  SOURCE_DYLIB_HASH="$(sha256_for_file "${SOURCE_DYLIB}")"
  ok "source dylib hash" "${SOURCE_DYLIB_HASH}"
  info "source dylib mtime" "$(mtime_display "${SOURCE_DYLIB}")"
  if strings "${SOURCE_DYLIB}" 2>/dev/null | grep -Fq "${RUNTIME_MARKER}"; then
    ok "source dylib marker" "${RUNTIME_MARKER}"
  else
    fail "source dylib marker" "${RUNTIME_MARKER} not found in ${SOURCE_DYLIB}; rebuild native plugin first"
  fi
fi

if [[ ! -f "${BUNDLED_DYLIB}" ]]; then
  fail "bundled dylib" "missing: ${BUNDLED_DYLIB}"
else
  BUNDLED_DYLIB_HASH="$(sha256_for_file "${BUNDLED_DYLIB}")"
  ok "bundled dylib hash" "${BUNDLED_DYLIB_HASH}"
  info "bundled dylib mtime" "$(mtime_display "${BUNDLED_DYLIB}")"
  if strings "${BUNDLED_DYLIB}" 2>/dev/null | grep -Fq "${RUNTIME_MARKER}"; then
    ok "bundled dylib marker" "${RUNTIME_MARKER}"
  else
    fail "bundled dylib marker" "${RUNTIME_MARKER} not found in ${BUNDLED_DYLIB}; Unity bundled a stale native plugin"
  fi
fi

if [[ -n "${SOURCE_DYLIB_HASH:-}" && -n "${BUNDLED_DYLIB_HASH:-}" ]]; then
  if [[ "${SOURCE_DYLIB_HASH}" == "${BUNDLED_DYLIB_HASH}" ]]; then
    ok "dylib freshness" "bundled dylib matches source dylib"
  else
    fail "dylib freshness" "bundled dylib hash differs from source dylib hash"
  fi
fi

section "Managed Assembly"
LATEST_SOURCE_RECORD="$(latest_source_record || true)"
LATEST_SOURCE_EPOCH="${LATEST_SOURCE_RECORD%% *}"
LATEST_SOURCE_PATH="${LATEST_SOURCE_RECORD#* }"
if [[ -z "${LATEST_SOURCE_RECORD}" || "${LATEST_SOURCE_EPOCH}" == "${LATEST_SOURCE_RECORD}" ]]; then
  fail "latest C# source" "no C# source files found under ${SOURCE_CS_ROOT}"
else
  info "latest C# source" "$(date -r "${LATEST_SOURCE_EPOCH}" -u '+%Y-%m-%dT%H:%M:%SZ') ${LATEST_SOURCE_PATH}"
fi

if source_contains_marker "${SOURCE_CS_ROOT}" "${RUNTIME_MARKER}"; then
  ok "source marker" "${RUNTIME_MARKER}"
else
  fail "source marker" "${RUNTIME_MARKER} not found under ${SOURCE_CS_ROOT}"
fi

if source_contains_marker "${SOURCE_CS_ROOT}" "${LEGACY_RUNTIME_MARKER}"; then
  info "legacy source marker" "${LEGACY_RUNTIME_MARKER}"
fi

if [[ -f "${PROJECT_MANAGED_DLL}" ]]; then
  PROJECT_MANAGED_HASH="$(sha256_for_file "${PROJECT_MANAGED_DLL}")"
  PROJECT_MANAGED_EPOCH="$(mtime_epoch "${PROJECT_MANAGED_DLL}")"
  ok "project managed hash" "${PROJECT_MANAGED_HASH}"
  info "project managed path" "${PROJECT_MANAGED_DLL}"
  info "project managed mtime" "$(mtime_display "${PROJECT_MANAGED_DLL}")"

  if [[ -n "${LATEST_SOURCE_EPOCH:-}" && "${PROJECT_MANAGED_EPOCH}" -lt "${LATEST_SOURCE_EPOCH}" ]]; then
    warn "project managed freshness" "intermediate TokenForge.Client.dll is older than latest C# source change; runtime verdict uses Player bundled DLL"
  else
    ok "project managed freshness" "project TokenForge.Client.dll is newer than or equal to latest C# source"
  fi

  PROJECT_MARKER_METHOD="$(contains_marker "${PROJECT_MANAGED_DLL}" "${RUNTIME_MARKER}" || true)"
  if [[ -n "${PROJECT_MARKER_METHOD}" ]]; then
    ok "project ScriptAssemblies marker" "${RUNTIME_MARKER} (${PROJECT_MARKER_METHOD})"
  else
    PROJECT_FOUND_MARKERS="$(managed_marker_candidates "${PROJECT_MANAGED_DLL}")"
    warn "project ScriptAssemblies marker" "${RUNTIME_MARKER} not found in intermediate ${PROJECT_MANAGED_DLL}; found markers: ${PROJECT_FOUND_MARKERS:-none}; runtime verdict uses Player bundled DLL"
  fi
else
  warn "project managed dll" "intermediate missing: ${PROJECT_MANAGED_DLL}; runtime verdict uses Player bundled DLL"
fi

if [[ ! -f "${BUNDLED_MANAGED_DLL}" ]]; then
  fail "bundled managed dll" "missing: ${BUNDLED_MANAGED_DLL}"
else
  BUNDLED_MANAGED_HASH="$(sha256_for_file "${BUNDLED_MANAGED_DLL}")"
  BUNDLED_MANAGED_EPOCH="$(mtime_epoch "${BUNDLED_MANAGED_DLL}")"
  ok "bundled managed hash" "${BUNDLED_MANAGED_HASH}"
  info "bundled managed path" "${BUNDLED_MANAGED_DLL}"
  info "bundled managed mtime" "$(mtime_display "${BUNDLED_MANAGED_DLL}")"

  if [[ -n "${LATEST_SOURCE_EPOCH:-}" && "${BUNDLED_MANAGED_EPOCH}" -lt "${LATEST_SOURCE_EPOCH}" ]]; then
    fail "managed freshness" "bundled TokenForge.Client.dll is older than latest C# source change"
  else
    ok "managed freshness" "bundled TokenForge.Client.dll is newer than or equal to latest C# source"
  fi

  BUNDLED_MARKER_METHOD="$(contains_marker "${BUNDLED_MANAGED_DLL}" "${RUNTIME_MARKER}" || true)"
  if [[ -n "${BUNDLED_MARKER_METHOD}" ]]; then
    ok "Player bundled DLL marker" "${RUNTIME_MARKER} (${BUNDLED_MARKER_METHOD})"
  else
    BUNDLED_FOUND_MARKERS="$(managed_marker_candidates "${BUNDLED_MANAGED_DLL}")"
    fail "Player bundled DLL marker" "${RUNTIME_MARKER} not found in bundled TokenForge.Client.dll; found markers: ${BUNDLED_FOUND_MARKERS:-none}"
  fi
fi

section "Verdict"
if [[ "${FAILED}" -ne 0 ]]; then
  if [[ "${APP_BUNDLE_CLASSIFICATION}" == "PRESENT" ]]; then
    APP_BUNDLE_CLASSIFICATION="STALE_ARTIFACT"
  fi
  echo "Artifact classification: ${APP_BUNDLE_CLASSIFICATION}"
  echo "Artifact freshness: STALE"
  exit 1
fi

echo "Artifact classification: OK"
echo "Artifact freshness: OK"

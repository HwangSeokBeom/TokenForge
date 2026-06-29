#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"
UNITY_PROJECT_PATH="${REPO_ROOT}/UnityClient"
BUILD_SETTINGS="${UNITY_PROJECT_PATH}/ProjectSettings/EditorBuildSettings.asset"
BOOTSTRAP_SCENE="Assets/_Project/Scenes/TokenForgeMain.unity"

BUILD_OUTPUT="${BUILD_OUTPUT:-/private/tmp/tokenforge-macos-build/TokenForge.app}"
BUILD_OUTPUT_DIR="$(dirname "${BUILD_OUTPUT}")"
BUILD_STAGING_ROOT="${BUILD_STAGING_ROOT:-${BUILD_OUTPUT_DIR}/.tokenforge-staging}"
STAGING_BUILD_OUTPUT="${STAGING_BUILD_OUTPUT:-${BUILD_STAGING_ROOT}/TokenForge.app}"
BUILD_STATUS_FILE="${BUILD_STATUS_FILE:-${BUILD_OUTPUT_DIR}/tokenforge-build-status.env}"
LOG_FILE="${LOG_FILE:-/private/tmp/tokenforge-macos-build.log}"
DEVELOPMENT_BUILD="${DEVELOPMENT_BUILD:-false}"
CLEAN_BUILD="${CLEAN_BUILD:-true}"
REBUILD_NATIVE_PLUGIN="${REBUILD_NATIVE_PLUGIN:-true}"
UNITY_BUILD_TIMEOUT_SECONDS="${UNITY_BUILD_TIMEOUT_SECONDS:-600}"
CLEANUP_SCRIPT="${SCRIPT_DIR}/tokenforge-clean-unity-processes.sh"
NATIVE_REBUILD_SCRIPT="${SCRIPT_DIR}/rebuild-desktop-companion-overlay-dylib.sh"
NEWTONSOFT_PACKAGE_CACHE_SCRIPT="${SCRIPT_DIR}/restore-unity-newtonsoft-package-cache.sh"
TIMEOUT_EXIT_CODE=124

cleanup_unity_build_processes() {
  if [[ -x "${CLEANUP_SCRIPT}" ]]; then
    TOKENFORGE_UNITY_CLEANUP_LOGS="${LOG_FILE}" UNITY_PATH="${UNITY_PATH}" "${CLEANUP_SCRIPT}" cleanup || true
  fi
}

trap cleanup_unity_build_processes EXIT
trap 'cleanup_unity_build_processes; exit 130' INT
trap 'cleanup_unity_build_processes; exit 143' TERM

print_summary() {
  local result="$1"
  echo "TokenForge macOS build smoke summary"
  echo "Build target: StandaloneOSX"
  echo "Output path: ${BUILD_OUTPUT}"
  echo "Staging path: ${STAGING_BUILD_OUTPUT}"
  echo "Result: ${result}"
  echo "Log path: ${LOG_FILE}"
  echo "Status path: ${BUILD_STATUS_FILE}"
}

sha256_for_file() {
  shasum -a 256 "$1" | awk '{print $1}'
}

write_build_status() {
  local result="$1"
  local detail="${2:-}"
  local previous_app_present="${3:-unknown}"
  mkdir -p "${BUILD_OUTPUT_DIR}"
  {
    printf 'RESULT=%s\n' "${result}"
    printf 'DETAIL=%s\n' "${detail// /_}"
    printf 'BUILD_OUTPUT=%s\n' "${BUILD_OUTPUT}"
    printf 'STAGING_BUILD_OUTPUT=%s\n' "${STAGING_BUILD_OUTPUT}"
    printf 'LOG_FILE=%s\n' "${LOG_FILE}"
    printf 'PREVIOUS_APP_PRESENT=%s\n' "${previous_app_present}"
    printf 'APP_PRESENT_AFTER=%s\n' "$([[ -d "${BUILD_OUTPUT}" ]] && echo true || echo false)"
    printf 'TIMESTAMP_UTC=%s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
  } > "${BUILD_STATUS_FILE}"
}

verify_player_native_plugin() {
  local player_bundle="${1:-${BUILD_OUTPUT}}"
  local source_dylib="${UNITY_PROJECT_PATH}/Assets/Plugins/macOS/libDesktopCompanionOverlay.dylib"
  local player_dylib="${player_bundle}/Contents/PlugIns/libDesktopCompanionOverlay.dylib"
  local source_hash
  local player_hash

  if [[ ! -f "${source_dylib}" ]]; then
    echo "Native dylib source output is missing after rebuild: ${source_dylib}" >&2
    print_summary "failure: native dylib source missing"
    exit 21
  fi

  if [[ ! -f "${player_dylib}" ]]; then
    echo "INFO [NativeBuild][COPIED_TO_PLAYER] status=missing path=${player_dylib}"
    echo "Player bundle is missing DesktopCompanionOverlay dylib: ${player_dylib}" >&2
    print_summary "failure: native dylib not bundled"
    exit 22
  fi

  source_hash="$(sha256_for_file "${source_dylib}")"
  player_hash="$(sha256_for_file "${player_dylib}")"
  echo "INFO [NativeBuild][COPIED_TO_PLAYER] status=present path=${player_dylib}"
  echo "INFO [NativeBuild][PLAYER_DYLIB_HASH] ${player_hash}"
  if [[ "${source_hash}" != "${player_hash}" ]]; then
    echo "Player bundled dylib hash does not match rebuilt source dylib." >&2
    echo "source=${source_dylib} hash=${source_hash}" >&2
    echo "player=${player_dylib} hash=${player_hash}" >&2
    print_summary "failure: stale player native dylib"
    exit 23
  fi
}

clean_staging_output_dir() {
  local staging_root="$1"
  echo "INFO [BuildPipeline][CLEAN_STAGING_OUTPUT] directory=${staging_root} canonicalOutput=${BUILD_OUTPUT}"
  rm -rf "${staging_root}"
  mkdir -p "${staging_root}"
  find "${staging_root}" -maxdepth 1 -type f -name 'mono_crash*.json' -print0 2>/dev/null \
    | while IFS= read -r -d '' mono_crash; do
        echo "INFO [BuildPipeline][CLEAN_STAGING_OUTPUT] removeMonoCrash=${mono_crash}"
        rm -f "${mono_crash}"
      done
}

replace_build_output_from_staging() {
  if [[ ! -d "${STAGING_BUILD_OUTPUT}" ]]; then
    echo "Staging app bundle is missing after successful Unity exit: ${STAGING_BUILD_OUTPUT}" >&2
    return 1
  fi

  if [[ ! -d "${BUILD_OUTPUT}" ]]; then
    echo "INFO [BuildPipeline][ATOMIC_REPLACE] mode=rename from=${STAGING_BUILD_OUTPUT} to=${BUILD_OUTPUT}"
    mv "${STAGING_BUILD_OUTPUT}" "${BUILD_OUTPUT}"
    rm -rf "${BUILD_STAGING_ROOT}"
    return 0
  fi

  if command -v clang >/dev/null 2>&1; then
    local swap_source
    local swap_helper
    swap_source="$(mktemp "${TMPDIR:-/tmp}/tokenforge-rename-swap.XXXXXX.c")"
    swap_helper="$(mktemp "${TMPDIR:-/tmp}/tokenforge-rename-swap.XXXXXX")"
    cat > "${swap_source}" <<'EOF'
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>

#ifndef RENAME_SWAP
#define RENAME_SWAP 0x00000002
#endif

int renameatx_np(int fromfd, const char *from, int tofd, const char *to, unsigned int flags);

int main(int argc, char **argv) {
  if (argc != 3) {
    fprintf(stderr, "usage: %s <from> <to>\n", argv[0]);
    return 64;
  }
  if (renameatx_np(AT_FDCWD, argv[1], AT_FDCWD, argv[2], RENAME_SWAP) != 0) {
    perror("renameatx_np(RENAME_SWAP)");
    return 1;
  }
  return 0;
}
EOF
    if clang "${swap_source}" -o "${swap_helper}" >/dev/null 2>&1; then
      echo "INFO [BuildPipeline][ATOMIC_REPLACE] mode=renameatx_np_swap from=${STAGING_BUILD_OUTPUT} to=${BUILD_OUTPUT}"
      if "${swap_helper}" "${STAGING_BUILD_OUTPUT}" "${BUILD_OUTPUT}"; then
        rm -rf "${STAGING_BUILD_OUTPUT}" "${BUILD_STAGING_ROOT}" "${swap_source}" "${swap_helper}"
        return 0
      fi
    fi
    rm -f "${swap_source}" "${swap_helper}"
  fi

  local previous_output="${BUILD_OUTPUT}.previous.$$"
  rm -rf "${previous_output}"

  echo "WARN [BuildPipeline][ATOMIC_REPLACE_FALLBACK] mode=backup_restore from=${STAGING_BUILD_OUTPUT} to=${BUILD_OUTPUT}"
  mv "${BUILD_OUTPUT}" "${previous_output}"

  if mv "${STAGING_BUILD_OUTPUT}" "${BUILD_OUTPUT}"; then
    rm -rf "${previous_output}" "${BUILD_STAGING_ROOT}"
    return 0
  fi

  echo "Failed to promote staging app bundle into canonical output." >&2
  if [[ -d "${previous_output}" && ! -d "${BUILD_OUTPUT}" ]]; then
    mv "${previous_output}" "${BUILD_OUTPUT}" || true
  fi
  return 1
}

upm_blocked_line() {
  [[ -f "${LOG_FILE}" ]] || return 1
  LC_ALL=C grep -aE '(Unity Package Manager|UPM|Unity-Upm|/tmp/Unity-Upm-[^[:space:]]+\.sock).*(EPERM|Operation not permitted|listen)|((EPERM|Operation not permitted).*(Unity Package Manager|UPM|Unity-Upm|/tmp/Unity-Upm-[^[:space:]]+\.sock))' "${LOG_FILE}" | head -1
}

summarize_upm_blocked() {
  local line
  line="$(upm_blocked_line || true)"
  echo "BLOCKED [BuildPipeline][UPM_BLOCKED] Unity Package Manager IPC listen failed with EPERM."
  if [[ -n "${line}" ]]; then
    echo "BLOCKED [BuildPipeline][UPM_BLOCKED_LOG_LINE] ${line}"
  else
    echo "BLOCKED [BuildPipeline][UPM_BLOCKED_LOG_LINE] no exact EPERM line found in ${LOG_FILE}"
  fi
}

print_unity_lock_diagnostics() {
  local lock_file="$1"
  echo "UnityClient/Temp/UnityLockfile exists: ${lock_file}" >&2
  echo "Clean rebuild is blocked until this lock is released." >&2
  echo "lsof holder probe:" >&2
  if command -v lsof >/dev/null 2>&1; then
    if ! lsof "${lock_file}" >&2; then
      echo "lsof could not read lock holders in this environment." >&2
    fi
  else
    echo "lsof is not available." >&2
  fi
  echo "Unity/UPM/Licensing/AssetImportWorker process probe:" >&2
  if command -v pgrep >/dev/null 2>&1; then
    pgrep -fl "Unity|Unity Hub|Unity Package Manager|UPM|Unity Licensing|LicensingClient|AssetImportWorker" >&2 \
      || echo "No Unity/UPM/Licensing/AssetImportWorker processes visible to pgrep, or process listing is denied." >&2
    echo "fileproviderd process probe:" >&2
    pgrep -fl "fileproviderd|FileProvider" >&2 \
      || echo "No fileproviderd/FileProvider processes visible to pgrep." >&2
  else
    echo "pgrep is not available." >&2
  fi
  echo "If fileproviderd appears above, the project may be in iCloud/File Provider/Dropbox/OneDrive sync storage." >&2
  echo "Safe manual delete guidance: quit Unity Editor/Hub/UPM/Licensing/AssetImportWorker first; if lsof shows no holder, then remove ${lock_file} manually and rebuild." >&2
}

run_with_timeout() {
  local timeout_seconds="$1"
  shift

  "$@" &
  local unity_pid=$!
  local start_epoch
  start_epoch="$(date +%s)"

  while kill -0 "${unity_pid}" >/dev/null 2>&1; do
    local now
    now="$(date +%s)"
    if [[ $((now - start_epoch)) -ge "${timeout_seconds}" ]]; then
      echo "Unity build command timed out after ${timeout_seconds}s; sending SIGTERM to pid ${unity_pid}" >&2
      kill -TERM "${unity_pid}" >/dev/null 2>&1 || true
      local waited=0
      while kill -0 "${unity_pid}" >/dev/null 2>&1 && [[ "${waited}" -lt 10 ]]; do
        sleep 1
        waited=$((waited + 1))
      done
      if kill -0 "${unity_pid}" >/dev/null 2>&1; then
        kill -KILL "${unity_pid}" >/dev/null 2>&1 || true
      fi
      wait "${unity_pid}" >/dev/null 2>&1 || true
      return "${TIMEOUT_EXIT_CODE}"
    fi
    sleep 1
  done

  wait "${unity_pid}"
}

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable: ${UNITY_PATH}" >&2
  echo "Set UNITY_PATH to a Unity ${TOKENFORGE_UNITY_VERSION} executable." >&2
  print_summary "failure"
  exit 1
fi

echo "INFO [BuildPipeline][PROCESS_PREFLIGHT] log=${LOG_FILE}"
echo "INFO [BuildPipeline][UNITY_VERSION_SELECTED] unityVersion=${TOKENFORGE_UNITY_VERSION} unityPath=${UNITY_PATH} unityAppPath=${UNITY_APP_PATH}"

if [[ ! -f "${UNITY_PROJECT_PATH}/${BOOTSTRAP_SCENE}" ]]; then
  echo "Bootstrap scene is missing: ${BOOTSTRAP_SCENE}" >&2
  print_summary "failure"
  exit 1
fi

if [[ ! -f "${BUILD_SETTINGS}" ]] || ! grep -q "path: ${BOOTSTRAP_SCENE}" "${BUILD_SETTINGS}"; then
  echo "Bootstrap scene is not present in EditorBuildSettings." >&2
  print_summary "failure"
  exit 1
fi

if [[ "${RESTORE_NEWTONSOFT_PACKAGE_CACHE:-true}" == "true" ]]; then
  if [[ ! -x "${NEWTONSOFT_PACKAGE_CACHE_SCRIPT}" ]]; then
    echo "Newtonsoft package cache restore script is missing or not executable: ${NEWTONSOFT_PACKAGE_CACHE_SCRIPT}" >&2
    print_summary "failure: Newtonsoft package cache restore script missing"
    exit 1
  fi

  UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH}" "${NEWTONSOFT_PACKAGE_CACHE_SCRIPT}"
fi

mkdir -p "${BUILD_OUTPUT_DIR}"
PREVIOUS_APP_PRESENT="$([[ -d "${BUILD_OUTPUT}" ]] && echo true || echo false)"
write_build_status "BUILD_STARTED" "staging_build_started" "${PREVIOUS_APP_PRESENT}"

LOCK_FILE="${UNITY_PROJECT_PATH}/Temp/UnityLockfile"
if [[ -f "${LOCK_FILE}" ]]; then
  echo "INFO [BuildPipeline][LOCKFILE_REMOVED] status=blocked path=${LOCK_FILE}"
  echo "Unity project lock detected: ${LOCK_FILE}" >&2
  print_unity_lock_diagnostics "${LOCK_FILE}"
  write_build_status "UNITY_LOCK_BLOCKED" "UnityLockfile_exists" "${PREVIOUS_APP_PRESENT}"
  print_summary "blocked: unity project lock"
  exit 2
fi

echo "INFO [BuildPipeline][LOCKFILE_REMOVED] status=not_present path=${LOCK_FILE}"

if [[ "${CLEAN_BUILD}" == "true" ]]; then
  clean_staging_output_dir "${BUILD_STAGING_ROOT}"
else
  mkdir -p "${BUILD_STAGING_ROOT}"
fi

if [[ "${REBUILD_NATIVE_PLUGIN}" == "true" ]]; then
  if [[ ! -x "${NATIVE_REBUILD_SCRIPT}" ]]; then
    echo "Native rebuild script is missing or not executable: ${NATIVE_REBUILD_SCRIPT}" >&2
    print_summary "failure: native rebuild script missing"
    exit 1
  fi

  "${NATIVE_REBUILD_SCRIPT}"
fi

if [[ -x "${CLEANUP_SCRIPT}" ]]; then
  TOKENFORGE_UNITY_CLEANUP_LOGS="${LOG_FILE}" UNITY_PATH="${UNITY_PATH}" "${CLEANUP_SCRIPT}" preflight || true
fi

BUILD_COMMAND=(
  "${UNITY_PATH}"
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -buildTarget StandaloneOSX \
  -executeMethod TokenForge.Editor.MacOSBuildSmokeCommand.Build \
  -buildOutput "${STAGING_BUILD_OUTPUT}" \
  -developmentBuild "${DEVELOPMENT_BUILD}" \
  -cleanBuild "${CLEAN_BUILD}" \
  -quit \
  -logFile "${LOG_FILE}"
)

echo "Unity build timeout seconds: ${UNITY_BUILD_TIMEOUT_SECONDS}"
if run_with_timeout "${UNITY_BUILD_TIMEOUT_SECONDS}" "${BUILD_COMMAND[@]}"; then
  cleanup_unity_build_processes
  verify_player_native_plugin "${STAGING_BUILD_OUTPUT}"
  if ! replace_build_output_from_staging; then
    write_build_status "BUILD_FAILED" "staging_promotion_failed" "${PREVIOUS_APP_PRESENT}"
    print_summary "failure: staging promotion failed"
    exit 24
  fi
  verify_player_native_plugin "${BUILD_OUTPUT}"
  write_build_status "SUCCESS" "canonical_app_replaced_from_staging" "${PREVIOUS_APP_PRESENT}"
  print_summary "success"
else
  build_exit=$?
  cleanup_unity_build_processes
  if [[ "${build_exit}" -eq "${TIMEOUT_EXIT_CODE}" ]]; then
    write_build_status "BUILD_FAILED" "timeout" "${PREVIOUS_APP_PRESENT}"
    print_summary "failure: timeout"
    exit "${TIMEOUT_EXIT_CODE}"
  fi
  if upm_blocked_line >/dev/null 2>&1; then
    summarize_upm_blocked
    rm -rf "${BUILD_STAGING_ROOT}"
    if [[ -d "${BUILD_OUTPUT}" ]]; then
      write_build_status "UPM_BLOCKED" "existing_app_preserved" "${PREVIOUS_APP_PRESENT}"
      print_summary "UPM_BLOCKED: existing app bundle preserved"
      exit 3
    fi
    write_build_status "ARTIFACT_MISSING_AFTER_BLOCKED_BUILD" "UPM_BLOCKED_before_player_bundle" "${PREVIOUS_APP_PRESENT}"
    print_summary "ARTIFACT_MISSING_AFTER_BLOCKED_BUILD"
    exit 4
  fi
  write_build_status "BUILD_FAILED" "unity_exit_${build_exit}" "${PREVIOUS_APP_PRESENT}"
  print_summary "failure"
  exit "${build_exit}"
fi

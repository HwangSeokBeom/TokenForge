#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"
UNITY_PROJECT_PATH="${REPO_ROOT}/UnityClient"
BUILD_SETTINGS="${UNITY_PROJECT_PATH}/ProjectSettings/EditorBuildSettings.asset"
BOOTSTRAP_SCENE="Assets/_Project/Scenes/TokenForgeMain.unity"

BUILD_OUTPUT="${BUILD_OUTPUT:-/tmp/tokenforge-macos-build/TokenForge.app}"
LOG_FILE="${LOG_FILE:-/tmp/tokenforge-macos-build.log}"
DEVELOPMENT_BUILD="${DEVELOPMENT_BUILD:-false}"
CLEAN_BUILD="${CLEAN_BUILD:-true}"
REBUILD_NATIVE_PLUGIN="${REBUILD_NATIVE_PLUGIN:-true}"
UNITY_BUILD_TIMEOUT_SECONDS="${UNITY_BUILD_TIMEOUT_SECONDS:-600}"
CLEANUP_SCRIPT="${SCRIPT_DIR}/tokenforge-clean-unity-processes.sh"
NATIVE_REBUILD_SCRIPT="${SCRIPT_DIR}/rebuild-desktop-companion-overlay-dylib.sh"
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
  echo "Result: ${result}"
  echo "Log path: ${LOG_FILE}"
}

sha256_for_file() {
  shasum -a 256 "$1" | awk '{print $1}'
}

verify_player_native_plugin() {
  local source_dylib="${UNITY_PROJECT_PATH}/Assets/Plugins/macOS/libDesktopCompanionOverlay.dylib"
  local player_dylib="${BUILD_OUTPUT}/Contents/PlugIns/libDesktopCompanionOverlay.dylib"
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

mkdir -p "$(dirname "${BUILD_OUTPUT}")"

LOCK_FILE="${UNITY_PROJECT_PATH}/Temp/UnityLockfile"
if [[ -f "${LOCK_FILE}" ]]; then
  echo "INFO [BuildPipeline][LOCKFILE_REMOVED] status=blocked path=${LOCK_FILE}"
  echo "Unity project lock detected: ${LOCK_FILE}" >&2
  echo "Clean rebuild is blocked until this lock is released." >&2
  echo "Processes that may hold the lock:" >&2
  if command -v lsof >/dev/null 2>&1; then
    if ! lsof "${LOCK_FILE}" >&2; then
      echo "lsof could not read lock holders in this environment." >&2
    fi
  else
    echo "lsof is not available." >&2
  fi
  echo "Unity-related process probe:" >&2
  if command -v pgrep >/dev/null 2>&1; then
    pgrep -fl "Unity|Unity Hub|Unity Licensing|LicensingClient" >&2 || echo "No Unity-related processes visible to pgrep, or process listing is denied." >&2
  else
    echo "pgrep is not available." >&2
  fi
  echo "Manual action required: quit Unity Editor/Hub/Licensing Client before deleting the lockfile." >&2
  echo "If fileproviderd appears above, move the project out of iCloud/File Provider/Dropbox/OneDrive sync paths before rebuilding." >&2
  print_summary "blocked: unity project lock"
  exit 2
fi

echo "INFO [BuildPipeline][LOCKFILE_REMOVED] status=not_present path=${LOCK_FILE}"

if [[ "${CLEAN_BUILD}" == "true" ]]; then
  rm -rf "${BUILD_OUTPUT}"
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
  -buildOutput "${BUILD_OUTPUT}" \
  -developmentBuild "${DEVELOPMENT_BUILD}" \
  -cleanBuild "${CLEAN_BUILD}" \
  -quit \
  -logFile "${LOG_FILE}"
)

echo "Unity build timeout seconds: ${UNITY_BUILD_TIMEOUT_SECONDS}"
if run_with_timeout "${UNITY_BUILD_TIMEOUT_SECONDS}" "${BUILD_COMMAND[@]}"; then
  cleanup_unity_build_processes
  verify_player_native_plugin
  print_summary "success"
else
  build_exit=$?
  cleanup_unity_build_processes
  if [[ "${build_exit}" -eq "${TIMEOUT_EXIT_CODE}" ]]; then
    print_summary "failure: timeout"
    exit "${TIMEOUT_EXIT_CODE}"
  fi
  print_summary "failure"
  exit "${build_exit}"
fi

#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${REPO_ROOT}/UnityClient"
BUILD_SETTINGS="${UNITY_PROJECT_PATH}/ProjectSettings/EditorBuildSettings.asset"
BOOTSTRAP_SCENE="Assets/_Project/Scenes/TokenForgeMain.unity"

UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity}"
BUILD_OUTPUT="${BUILD_OUTPUT:-/tmp/tokenforge-macos-build/TokenForge.app}"
LOG_FILE="${LOG_FILE:-/tmp/tokenforge-macos-build.log}"
DEVELOPMENT_BUILD="${DEVELOPMENT_BUILD:-false}"
CLEAN_BUILD="${CLEAN_BUILD:-true}"

print_summary() {
  local result="$1"
  echo "TokenForge macOS build smoke summary"
  echo "Build target: StandaloneOSX"
  echo "Output path: ${BUILD_OUTPUT}"
  echo "Result: ${result}"
  echo "Log path: ${LOG_FILE}"
}

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable: ${UNITY_PATH}" >&2
  echo "Set UNITY_PATH to a Unity 2022.3.0f1 executable." >&2
  print_summary "failure"
  exit 1
fi

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
  echo "Unity project lock detected: ${LOCK_FILE}" >&2
  echo "Processes that may hold the lock:" >&2
  lsof "${LOCK_FILE}" 2>/dev/null || ps aux | grep -i "[U]nity" >&2 || true
  print_summary "blocked: unity project lock"
  exit 2
fi

if [[ "${CLEAN_BUILD}" == "true" ]]; then
  rm -rf "${BUILD_OUTPUT}"
fi

if "${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -buildTarget StandaloneOSX \
  -executeMethod TokenForge.Editor.MacOSBuildSmokeCommand.Build \
  -buildOutput "${BUILD_OUTPUT}" \
  -developmentBuild "${DEVELOPMENT_BUILD}" \
  -cleanBuild "${CLEAN_BUILD}" \
  -quit \
  -logFile "${LOG_FILE}"; then
  print_summary "success"
else
  print_summary "failure"
  exit 1
fi

#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${REPO_ROOT}/UnityClient"

UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="${LOG_FILE:-/tmp/tokenforge-release-metadata.log}"

print_summary() {
  local result="$1"
  echo "TokenForge release metadata validation summary"
  echo "Product name: TokenForge"
  echo "Bundle identifier: com.tokenforge.client"
  echo "Version: 0.18.0"
  echo "Build number: 18"
  echo "Bootstrap scene: Assets/_Project/Scenes/Bootstrap.unity"
  echo "Entitlements: BuildSupport/macOS/TokenForge.entitlements"
  echo "App icon: Assets/_Project/Art/AppIcon/TokenForgeReleaseIcon.png"
  echo "Log path: ${LOG_FILE}"
  echo "Result: ${result}"
}

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable. Set UNITY_PATH to Unity 2022.3.0f1." >&2
  print_summary "failure"
  exit 1
fi

if "${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -executeMethod TokenForge.Editor.ReleaseMetadataValidator.ValidateForRelease \
  -quit \
  -logFile "${LOG_FILE}"; then
  print_summary "success"
else
  print_summary "failure"
  exit 1
fi

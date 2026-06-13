#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${REPO_ROOT}/UnityClient"

OUTPUT_PATH="${OUTPUT_PATH:-${UNITY_PROJECT_PATH}/artifacts/client-contract/unity-safe-sync-contract-v1.bundle.json}"
LOG_FILE="${LOG_FILE:-/tmp/tokenforge-contract-export.log}"

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable: ${UNITY_PATH}" >&2
  echo "Set UNITY_PATH to a Unity 6000.4.10f1 executable." >&2
  exit 1
fi

"${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput "${OUTPUT_PATH}" \
  -quit \
  -logFile "${LOG_FILE}"

echo "Safe Sync contract bundle exported to ${OUTPUT_PATH}"

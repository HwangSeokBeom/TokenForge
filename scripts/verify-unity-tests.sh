#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity}"
EDITMODE_RESULTS="${EDITMODE_RESULTS:-/tmp/tokenforge-editmode-results.xml}"
PLAYMODE_RESULTS="${PLAYMODE_RESULTS:-/tmp/tokenforge-playmode-results.xml}"
EDITMODE_LOG="${EDITMODE_LOG:-/tmp/tokenforge-unity-editmode.log}"
PLAYMODE_LOG="${PLAYMODE_LOG:-/tmp/tokenforge-unity-playmode.log}"
RUN_EDITMODE="${RUN_EDITMODE:-true}"
RUN_PLAYMODE="${RUN_PLAYMODE:-true}"

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable: ${UNITY_PATH}" >&2
  exit 1
fi

parse_results() {
  local result_path="$1"
  /usr/bin/python3 - "${result_path}" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()
passed = int(root.attrib.get("passed", "0"))
failed = int(root.attrib.get("failed", "0"))
inconclusive = int(root.attrib.get("inconclusive", "0"))
skipped = int(root.attrib.get("skipped", "0"))
total = int(root.attrib.get("total", passed + failed + inconclusive + skipped))
print(f"total={total} passed={passed} failed={failed} inconclusive={inconclusive} skipped={skipped}")
if failed > 0:
    raise SystemExit(2)
PY
}

run_platform() {
  local platform="$1"
  local result_path="$2"
  local log_path="$3"
  mkdir -p "$(dirname "${result_path}")" "$(dirname "${log_path}")"
  rm -f "${result_path}"

  "${UNITY_PATH}" \
    -batchmode \
    -nographics \
    -projectPath "${UNITY_PROJECT_PATH}" \
    -runTests \
    -testPlatform "${platform}" \
    -testResults "${result_path}" \
    -logFile "${log_path}"
  local unity_exit=$?

  echo "Unity ${platform} exit code: ${unity_exit}"
  echo "Unity ${platform} log: ${log_path}"
  echo "Unity ${platform} XML: ${result_path}"

  if [[ ! -s "${result_path}" ]]; then
    echo "Unity ${platform} test run completed with exit code ${unity_exit}, but fresh result XML was not created." >&2
    return 1
  fi

  parse_results "${result_path}"
  local parse_exit=$?
  if [[ ${unity_exit} -ne 0 ]]; then
    return "${unity_exit}"
  fi

  return "${parse_exit}"
}

overall=0
if [[ "${RUN_EDITMODE}" == "true" ]]; then
  run_platform editmode "${EDITMODE_RESULTS}" "${EDITMODE_LOG}" || overall=$?
fi

if [[ "${RUN_PLAYMODE}" == "true" ]]; then
  run_platform playmode "${PLAYMODE_RESULTS}" "${PLAYMODE_LOG}" || overall=$?
fi

exit "${overall}"

#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
SIMPLE_LOG_FILE="${SIMPLE_LOG_FILE:-/tmp/tokenforge-unity-simple-license-smoke-shaped-compare.log}"
EDITMODE_LOG_FILE="${EDITMODE_LOG_FILE:-/tmp/tokenforge-unity-editmode-license-smoke.log}"
EDITMODE_RESULTS="${EDITMODE_RESULTS:-/tmp/tokenforge-unity-editmode-license-smoke-results.xml}"
TEST_FILTER="${TEST_FILTER:-TokenForge.Client.Tests.__EditModeLicenseProbeNoSuchTest__}"

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable: ${UNITY_PATH}" >&2
  echo "Set UNITY_PATH to a Unity 6000.4.10f1 executable." >&2
  exit 1
fi

print_command() {
  local label="$1"
  shift

  echo "${label}:"
  printf ' %q' "$@"
  printf '\n'
}

print_processes() {
  local label="$1"
  local process_output
  local process_status

  echo "Unity.Licensing.Client processes ${label}:"
  if command -v pgrep >/dev/null 2>&1; then
    process_output="$(pgrep -fl "Unity\.Licensing\.Client" 2>&1)"
    process_status=$?
    if [[ "${process_status}" -eq 0 ]]; then
      echo "${process_output}"
    elif [[ "${process_status}" -eq 1 ]]; then
      echo "none"
    else
      echo "process listing unavailable: ${process_output}"
    fi
  else
    echo "pgrep is not available"
  fi
}

print_relevant_log_lines() {
  local log_file="$1"

  if [[ ! -f "${log_file}" ]]; then
    echo "Unity log was not produced: ${log_file}" >&2
    return
  fi

  echo "Relevant Unity licensing/IPC log lines from ${log_file}:"
  grep -E "Licens|LicenseClient|Unity-LicenseClient|Unity\.Licensing\.Client|IPC|Timed-out|abort|channel" "${log_file}" || echo "No licensing/IPC lines found."
}

print_waited_channel() {
  local log_file="$1"
  local channel

  if [[ ! -f "${log_file}" ]]; then
    echo "Waited licensing channel: unavailable; log missing"
    return
  fi

  channel="$(grep -Eo 'waiting for channel: "[^"]+"' "${log_file}" | tail -1 | sed -E 's/.*"([^"]+)"/\1/' || true)"
  if [[ -n "${channel}" ]]; then
    echo "Waited licensing channel: ${channel}"
  else
    echo "Waited licensing channel: none reported"
  fi
}

run_unity_probe() {
  local label="$1"
  local log_file="$2"
  shift 2
  local command=("$@")

  mkdir -p "$(dirname "${log_file}")"
  rm -f "${log_file}"

  print_command "${label} command" "${command[@]}"
  echo "${label} log: ${log_file}"
  print_processes "before ${label}"

  "${command[@]}"
  local unity_exit=$?

  print_processes "after ${label}"
  print_relevant_log_lines "${log_file}"
  print_waited_channel "${log_file}"
  echo "${label} exit code: ${unity_exit}"

  return "${unity_exit}"
}

mkdir -p "$(dirname "${SIMPLE_LOG_FILE}")" "$(dirname "${EDITMODE_LOG_FILE}")" "$(dirname "${EDITMODE_RESULTS}")"
rm -f "${EDITMODE_RESULTS}"

SIMPLE_COMMAND=(
  "${UNITY_PATH}"
  -batchmode
  -nographics
  -projectPath "${UNITY_PROJECT_PATH}"
  -quit
  -logFile "${SIMPLE_LOG_FILE}"
)

EDITMODE_COMMAND=(
  "${UNITY_PATH}"
  -batchmode
  -nographics
  -projectPath "${UNITY_PROJECT_PATH}"
  -runTests
  -testPlatform editmode
  -testResults "${EDITMODE_RESULTS}"
  -testFilter "${TEST_FILTER}"
  -logFile "${EDITMODE_LOG_FILE}"
)

run_unity_probe "Simple license smoke shape" "${SIMPLE_LOG_FILE}" "${SIMPLE_COMMAND[@]}"
simple_exit=$?

run_unity_probe "EditMode license smoke shape" "${EDITMODE_LOG_FILE}" "${EDITMODE_COMMAND[@]}"
editmode_exit=$?

echo "EditMode license smoke XML: ${EDITMODE_RESULTS}"
if [[ -s "${EDITMODE_RESULTS}" ]]; then
  echo "EditMode-shaped probe produced XML."
else
  echo "EditMode-shaped probe did not produce XML."
fi

echo "Licensing shape comparison:"
if [[ "${simple_exit}" -eq 0 && "${editmode_exit}" -eq 0 ]]; then
  echo "simple smoke and EditMode-shaped smoke both reached Unity successfully."
elif [[ "${simple_exit}" -eq 0 && "${editmode_exit}" -ne 0 ]]; then
  echo "simple smoke reached Unity, but EditMode-shaped smoke failed; -runTests/EditMode invocation shape changes behavior on this machine."
elif [[ "${simple_exit}" -ne 0 && "${editmode_exit}" -eq 0 ]]; then
  echo "simple smoke failed, but EditMode-shaped smoke reached Unity; licensing behavior differs by invocation shape."
else
  echo "simple smoke and EditMode-shaped smoke both failed; licensing is not stable enough to isolate -runTests/EditMode behavior."
fi

if [[ "${editmode_exit}" -eq 199 ]]; then
  echo "EditMode license smoke result: failed at Unity LicensingClient IPC gate (exit 199)" >&2
elif [[ "${editmode_exit}" -ne 0 ]]; then
  echo "EditMode license smoke result: failed" >&2
else
  echo "EditMode license smoke result: success"
fi

if [[ "${simple_exit}" -ne 0 ]]; then
  exit "${simple_exit}"
fi

exit "${editmode_exit}"

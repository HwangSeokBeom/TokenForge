#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="${LOG_FILE:-/tmp/tokenforge-unity-license-smoke.log}"

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
  if [[ ! -f "${LOG_FILE}" ]]; then
    echo "Unity log was not produced: ${LOG_FILE}" >&2
    return
  fi

  echo "Relevant Unity licensing/IPC log lines:"
  grep -E "Licens|LicenseClient|Unity-LicenseClient|Unity\.Licensing\.Client|IPC|Timed-out|abort" "${LOG_FILE}" || echo "No licensing/IPC lines found."
}

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable: ${UNITY_PATH}" >&2
  echo "Set UNITY_PATH to a Unity 2022.3.0f1 executable." >&2
  exit 1
fi

mkdir -p "$(dirname "${LOG_FILE}")"
rm -f "${LOG_FILE}"

COMMAND=(
  "${UNITY_PATH}"
  -batchmode
  -nographics
  -projectPath "${UNITY_PROJECT_PATH}"
  -quit
  -logFile "${LOG_FILE}"
)

echo "Unity license smoke command:"
printf ' %q' "${COMMAND[@]}"
printf '\n'
echo "Unity license smoke log: ${LOG_FILE}"

print_processes "before"

"${COMMAND[@]}"
unity_exit=$?

print_processes "after"
print_relevant_log_lines

echo "Unity license smoke exit code: ${unity_exit}"

if [[ "${unity_exit}" -eq 0 ]]; then
  echo "Unity license smoke result: success"
  exit 0
fi

if [[ "${unity_exit}" -eq 199 ]]; then
  echo "Unity license smoke result: failed at Unity LicensingClient IPC gate (exit 199)" >&2
else
  echo "Unity license smoke result: failed" >&2
fi

exit "${unity_exit}"

#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="${LOG_FILE:-/tmp/tokenforge-unity-license-smoke.log}"
LICENSE_SMOKE_TIMEOUT_SECONDS="${LICENSE_SMOKE_TIMEOUT_SECONDS:-120}"
CLEANUP_SCRIPT="${SCRIPT_DIR}/tokenforge-clean-unity-processes.sh"
TIMEOUT_EXIT_CODE=124

cleanup_unity_smoke_processes() {
  if [[ -x "${CLEANUP_SCRIPT}" ]]; then
    TOKENFORGE_UNITY_CLEANUP_LOGS="${LOG_FILE}" UNITY_PATH="${UNITY_PATH}" "${CLEANUP_SCRIPT}" cleanup || true
  fi
}

trap cleanup_unity_smoke_processes EXIT
trap 'cleanup_unity_smoke_processes; exit 130' INT
trap 'cleanup_unity_smoke_processes; exit 143' TERM

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
  grep -E "Licens|LicenseClient|Unity-LicenseClient|Unity\.Licensing\.Client|IPC|Timed-out|abort|error CS[0-9]+|Scripts have compiler errors|compilation|Compilation" "${LOG_FILE}" || echo "No licensing/IPC/compiler lines found."
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
      echo "INFO [UnityLicenseSmoke] timeout seconds=${timeout_seconds} pid=${unity_pid}" >&2
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
if [[ -x "${CLEANUP_SCRIPT}" ]]; then
  TOKENFORGE_UNITY_CLEANUP_LOGS="${LOG_FILE}" UNITY_PATH="${UNITY_PATH}" "${CLEANUP_SCRIPT}" preflight || true
fi

echo "INFO [UnityLicenseSmoke] start timeoutSeconds=${LICENSE_SMOKE_TIMEOUT_SECONDS}"
run_with_timeout "${LICENSE_SMOKE_TIMEOUT_SECONDS}" "${COMMAND[@]}"
unity_exit=$?
cleanup_unity_smoke_processes

print_processes "after"
print_relevant_log_lines

echo "Unity license smoke exit code: ${unity_exit}"

compiler_errors="NO"
if [[ -s "${LOG_FILE}" ]] && grep -qE "error CS[0-9]+|Scripts have compiler errors" "${LOG_FILE}"; then
  compiler_errors="YES"
fi
echo "TOKENFORGE_VERIFY_COMPILER_ERRORS=${compiler_errors}"

if [[ "${unity_exit}" -eq 0 ]]; then
  echo "Unity license smoke result: success"
  exit 0
fi

if [[ "${compiler_errors}" == "YES" ]]; then
  echo "TOKENFORGE_VERIFY_STATUS=BLOCKED"
  echo "TOKENFORGE_VERIFY_FAILURE_KIND=compiler"
  echo "Unity license smoke result: compiler failure after licensing gate" >&2
  exit "${unity_exit}"
fi

if [[ "${unity_exit}" -eq 199 ]]; then
  echo "TOKENFORGE_VERIFY_STATUS=BLOCKED"
  echo "TOKENFORGE_VERIFY_FAILURE_KIND=license"
  echo "Unity license smoke result: failed at Unity LicensingClient IPC gate (exit 199)" >&2
elif [[ "${unity_exit}" -eq "${TIMEOUT_EXIT_CODE}" ]]; then
  echo "TOKENFORGE_VERIFY_STATUS=BLOCKED"
  echo "TOKENFORGE_VERIFY_FAILURE_KIND=license"
  echo "Unity license smoke result: timeout" >&2
else
  echo "TOKENFORGE_VERIFY_STATUS=BLOCKED"
  echo "TOKENFORGE_VERIFY_FAILURE_KIND=license"
  echo "Unity license smoke result: failed" >&2
fi

exit "${unity_exit}"

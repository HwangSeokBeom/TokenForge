#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
EDITMODE_RESULTS="${EDITMODE_RESULTS:-/tmp/tokenforge-editmode-results.xml}"
PLAYMODE_RESULTS="${PLAYMODE_RESULTS:-/tmp/tokenforge-playmode-results.xml}"
EDITMODE_LOG="${EDITMODE_LOG:-/tmp/tokenforge-unity-editmode.log}"
PLAYMODE_LOG="${PLAYMODE_LOG:-/tmp/tokenforge-unity-playmode.log}"
RUN_EDITMODE="${RUN_EDITMODE:-true}"
RUN_PLAYMODE="${RUN_PLAYMODE:-true}"
UNITY_TEST_TIMEOUT_SECONDS="${UNITY_TEST_TIMEOUT_SECONDS:-300}"
EDITMODE_FILTER="${EDITMODE_FILTER:-}"
PLAYMODE_FILTER="${PLAYMODE_FILTER:-}"
TIMEOUT_EXIT_CODE=124
CLEANUP_SCRIPT="${SCRIPT_DIR}/tokenforge-clean-unity-processes.sh"

cleanup_unity_test_processes() {
  if [[ -x "${CLEANUP_SCRIPT}" ]]; then
    TOKENFORGE_UNITY_CLEANUP_LOGS="${EDITMODE_LOG}:${PLAYMODE_LOG}" UNITY_PATH="${UNITY_PATH}" "${CLEANUP_SCRIPT}" cleanup || true
  fi
}

trap cleanup_unity_test_processes EXIT
trap 'cleanup_unity_test_processes; exit 130' INT
trap 'cleanup_unity_test_processes; exit 143' TERM

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable: ${UNITY_PATH}" >&2
  exit 1
fi

if ! [[ "${UNITY_TEST_TIMEOUT_SECONDS}" =~ ^[0-9]+$ ]] || [[ "${UNITY_TEST_TIMEOUT_SECONDS}" -le 0 ]]; then
  echo "UNITY_TEST_TIMEOUT_SECONDS must be a positive integer, got: ${UNITY_TEST_TIMEOUT_SECONDS}" >&2
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

print_command() {
  local label="$1"
  shift

  echo "${label}:"
  printf ' %q' "$@"
  printf '\n'
}

print_relevant_log_tail() {
  local log_path="$1"

  if [[ ! -f "${log_path}" ]]; then
    echo "Unity log does not exist yet: ${log_path}" >&2
    return 0
  fi

  echo "Last 120 relevant Unity log lines:" >&2
  if ! grep -E "Licens|LicenseClient|IPC|Timed-out|timeout|abort|test result|XML|Exception|Error|Failed|Passed|NUnit|TestRunner|Running tests|Executing tests|LogAssemblyErrors|Domain Reload|compilation|Compilation|NativeDashboardBridgeTests|RepositoryCompanionProfileTests|ApprovedActivityAnalysisViewModel|DesktopCompanionOverlay|AppKit" "${log_path}" | tail -n 120 >&2; then
    tail -n 120 "${log_path}" >&2 || true
  fi
}

xml_freshness_status() {
  local result_path="$1"
  local start_epoch="$2"

  if [[ ! -e "${result_path}" ]]; then
    echo "missing"
    return 1
  fi

  if [[ ! -s "${result_path}" ]]; then
    echo "present but empty"
    return 1
  fi

  local result_epoch
  result_epoch="$(stat -f '%m' "${result_path}")"
  if [[ "${result_epoch}" -lt "${start_epoch}" ]]; then
    echo "present but stale"
    return 1
  fi

  echo "present and fresh"
  return 0
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
      echo "Unity test command timed out after ${timeout_seconds}s; sending SIGTERM to pid ${unity_pid}" >&2
      kill -TERM "${unity_pid}" >/dev/null 2>&1 || true

      local waited=0
      while kill -0 "${unity_pid}" >/dev/null 2>&1 && [[ "${waited}" -lt 10 ]]; do
        sleep 1
        waited=$((waited + 1))
      done

      if kill -0 "${unity_pid}" >/dev/null 2>&1; then
        echo "Unity pid ${unity_pid} survived SIGTERM after ${waited}s; sending SIGKILL" >&2
        kill -KILL "${unity_pid}" >/dev/null 2>&1 || true
      fi

      wait "${unity_pid}" >/dev/null 2>&1 || true
      return "${TIMEOUT_EXIT_CODE}"
    fi

    sleep 1
  done

  wait "${unity_pid}"
}

run_platform() {
  local platform="$1"
  local result_path="$2"
  local log_path="$3"
  local test_filter="$4"
  mkdir -p "$(dirname "${result_path}")" "$(dirname "${log_path}")"
  rm -f "${result_path}"

  local command=(
    "${UNITY_PATH}"
    -batchmode \
    -nographics \
    -projectPath "${UNITY_PROJECT_PATH}" \
    -runTests \
    -testPlatform "${platform}" \
    -testResults "${result_path}" \
    -logFile "${log_path}"
  )

  if [[ -n "${test_filter}" ]]; then
    command+=(-testFilter "${test_filter}")
  fi

  print_command "Unity ${platform} command" "${command[@]}"
  if [[ -x "${CLEANUP_SCRIPT}" ]]; then
    TOKENFORGE_UNITY_CLEANUP_LOGS="${log_path}" UNITY_PATH="${UNITY_PATH}" "${CLEANUP_SCRIPT}" preflight || true
  fi
  if [[ -n "${test_filter}" ]]; then
    echo "Unity ${platform} filter: ${test_filter}"
  else
    echo "Unity ${platform} filter: <none>"
  fi
  echo "Unity ${platform} timeout seconds: ${UNITY_TEST_TIMEOUT_SECONDS}"

  local start_epoch
  start_epoch="$(date +%s)"

  run_with_timeout "${UNITY_TEST_TIMEOUT_SECONDS}" "${command[@]}"
  local unity_exit=$?
  cleanup_unity_test_processes

  echo "Unity ${platform} exit code: ${unity_exit}"
  if [[ "${unity_exit}" -eq "${TIMEOUT_EXIT_CODE}" ]]; then
    echo "Unity ${platform} timeout result: timed out after ${UNITY_TEST_TIMEOUT_SECONDS}s"
  else
    echo "Unity ${platform} timeout result: completed before timeout"
  fi
  echo "Unity ${platform} log: ${log_path}"
  echo "Unity ${platform} XML: ${result_path}"
  local xml_status
  xml_status="$(xml_freshness_status "${result_path}" "${start_epoch}")"
  local xml_status_exit=$?
  echo "Unity ${platform} XML freshness: ${xml_status}"

  if [[ "${unity_exit}" -eq "${TIMEOUT_EXIT_CODE}" ]]; then
    print_relevant_log_tail "${log_path}"
    return "${TIMEOUT_EXIT_CODE}"
  fi

  if [[ "${xml_status_exit}" -ne 0 ]]; then
    echo "Unity ${platform} test run exited before producing fresh result XML." >&2
    echo "Unity exit code: ${unity_exit}" >&2
    echo "Expected XML: ${result_path}" >&2
    echo "Log path: ${log_path}" >&2
    echo "XML freshness: ${xml_status}" >&2
    print_relevant_log_tail "${log_path}"
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
  run_platform editmode "${EDITMODE_RESULTS}" "${EDITMODE_LOG}" "${EDITMODE_FILTER}" || overall=$?
fi

if [[ "${RUN_PLAYMODE}" == "true" ]]; then
  run_platform playmode "${PLAYMODE_RESULTS}" "${PLAYMODE_LOG}" "${PLAYMODE_FILTER}" || overall=$?
fi

exit "${overall}"

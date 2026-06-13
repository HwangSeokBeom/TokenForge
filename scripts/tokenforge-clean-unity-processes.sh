#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"

ACTION="${1:-cleanup}"
WAIT_SECONDS="${TOKENFORGE_UNITY_CLEANUP_WAIT_SECONDS:-3}"
LOG_FILES="${TOKENFORGE_UNITY_CLEANUP_LOGS:-}"
LOG_MAX_AGE_SECONDS="${TOKENFORGE_UNITY_LOG_MAX_AGE_SECONDS:-900}"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
LOCK_FILE="${UNITY_PROJECT_PATH}/Temp/UnityLockfile"

unity_pattern() {
  printf '%s' "${UNITY_APP_PATH}"
}

manual_command() {
  printf 'pkill -TERM -f "VBCSCompiler.dll" || true; pkill -TERM -f "Unity.Licensing.Client" || true; pkill -TERM -f "%s" || true; sleep 3; pgrep -fl "Unity.Licensing.Client|Unity Hub|Unity|VBCSCompiler|dotnet"' "${UNITY_APP_PATH}"
}

unity_batchmode_pattern() {
  printf '%s' "(${UNITY_PATH}.*-batchmode|-batchmode.*${UNITY_PATH})"
}

process_lines() {
  if ! command -v pgrep >/dev/null 2>&1; then
    echo "process listing unavailable: pgrep is not available"
    return 2
  fi

  pgrep -fl "Unity\.Licensing\.Client|VBCSCompiler\.dll|${UNITY_APP_PATH}|Unity Hub" 2>&1
}

license_pids() {
  if ! command -v pgrep >/dev/null 2>&1; then
    return 2
  fi

  pgrep -f "Unity\.Licensing\.Client" 2>/dev/null
}

safe_kill_pattern() {
  local pattern="$1"
  local description="$2"
  local pids

  if ! command -v pgrep >/dev/null 2>&1; then
    echo "INFO [UnityProcessCleanup] skipped description=${description} reason=pgrep_unavailable"
    return 0
  fi

  pids="$(pgrep -f "${pattern}" 2>/dev/null || true)"
  if [[ -z "${pids}" ]]; then
    echo "INFO [UnityProcessCleanup] no_processes description=${description}"
    return 0
  fi

  echo "INFO [UnityProcessCleanup] terminate description=${description} pids=$(echo "${pids}" | tr '\n' ',' | sed 's/,$//')"
  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    kill -TERM "${pid}" >/dev/null 2>&1 || true
  done <<< "${pids}"
}

safe_kill_log_pids() {
  [[ -n "${LOG_FILES}" ]] || return 0

  local log_path
  IFS=':' read -r -a log_paths <<< "${LOG_FILES}"
  for log_path in "${log_paths[@]}"; do
    [[ -s "${log_path}" ]] || continue
    if [[ "${LOG_MAX_AGE_SECONDS}" =~ ^[0-9]+$ ]] && [[ "${LOG_MAX_AGE_SECONDS}" -gt 0 ]]; then
      local modified_epoch
      local now_epoch
      modified_epoch="$(stat -f '%m' "${log_path}" 2>/dev/null || echo 0)"
      now_epoch="$(date +%s)"
      if [[ "${modified_epoch}" -le 0 || $((now_epoch - modified_epoch)) -gt "${LOG_MAX_AGE_SECONDS}" ]]; then
        echo "INFO [UnityProcessCleanup] skip_log_pids log=${log_path} reason=staleLog ageSeconds=$((now_epoch - modified_epoch))"
        continue
      fi
    fi
    local pids
    pids="$(grep -Eo 'LicensingClient \(PId: [0-9]+\)|Unity.Licensing.Client.*PId: [0-9]+' "${log_path}" 2>/dev/null | grep -Eo '[0-9]+' | sort -u || true)"
    if [[ -z "${pids}" ]]; then
      continue
    fi

    echo "INFO [UnityProcessCleanup] terminate source=unity_log log=${log_path} pids=$(echo "${pids}" | tr '\n' ',' | sed 's/,$//')"
    while IFS= read -r pid; do
      [[ -n "${pid}" ]] || continue
      kill -TERM "${pid}" >/dev/null 2>&1 || true
    done <<< "${pids}"
  done
}

print_process_report() {
  local label="$1"
  local output
  local status

  echo "INFO [BuildPipeline][PROCESS_PREFLIGHT] label=${label} unityPath=${UNITY_PATH}"
  echo "INFO [BuildPipeline][UNITY_VERSION_SELECTED] unityVersion=${TOKENFORGE_UNITY_VERSION} unityPath=${UNITY_PATH} unityAppPath=${UNITY_APP_PATH}"
  if [[ -f "${LOCK_FILE}" ]]; then
    echo "INFO [BuildPipeline][LOCKFILE_REMOVED] status=blocked path=${LOCK_FILE}"
    if command -v lsof >/dev/null 2>&1; then
      echo "TOKENFORGE_VERIFY_LOCKFILE_HOLDERS=$(lsof "${LOCK_FILE}" 2>&1 | tr '\n' ';' | sed 's/;$//')"
    else
      echo "TOKENFORGE_VERIFY_LOCKFILE_HOLDERS=lsof_unavailable"
    fi
  else
    echo "INFO [BuildPipeline][LOCKFILE_REMOVED] status=not_present path=${LOCK_FILE}"
    echo "TOKENFORGE_VERIFY_LOCKFILE_HOLDERS="
  fi
  local license_output
  local license_status
  license_output="$(license_pids)"
  license_status=$?
  local license_key="TOKENFORGE_VERIFY_LICENSE_PIDS_BEFORE"
  if [[ "${label}" == "post_cleanup" ]]; then
    license_key="TOKENFORGE_VERIFY_LICENSE_PIDS_AFTER"
  fi
  if [[ "${license_status}" -eq 0 ]]; then
    echo "${license_key}=$(printf '%s' "${license_output}" | tr '\n' ',' | sed 's/,$//')"
  elif [[ "${license_status}" -eq 1 ]]; then
    echo "${license_key}="
  else
    echo "${license_key}=UNKNOWN"
  fi
  if [[ "${label}" == "post_cleanup" ]]; then
    echo "TOKENFORGE_VERIFY_CLEANUP_ATTEMPTED=YES"
  else
    echo "TOKENFORGE_VERIFY_CLEANUP_ATTEMPTED=NO"
  fi
  output="$(process_lines)"
  status=$?
  if [[ "${status}" -eq 0 ]]; then
    local count
    count="$(printf '%s\n' "${output}" | sed '/^[[:space:]]*$/d' | wc -l | tr -d ' ')"
    echo "TOKENFORGE_VERIFY_BLOCKING_PROCESS_COUNT=${count}"
    echo "TOKENFORGE_VERIFY_BLOCKING_PROCESSES=$(printf '%s' "${output}" | tr '\n' ';' | sed 's/;$//')"
    if [[ "${label}" == "post_cleanup" ]]; then
      echo "TOKENFORGE_VERIFY_PROCESS_CLEANUP=BLOCKED"
      echo "TOKENFORGE_VERIFY_CLEANUP_RESULT=BLOCKED"
    fi
  elif [[ "${status}" -eq 1 ]]; then
    echo "TOKENFORGE_VERIFY_BLOCKING_PROCESS_COUNT=0"
    echo "TOKENFORGE_VERIFY_BLOCKING_PROCESSES="
    if [[ "${label}" == "post_cleanup" ]]; then
      echo "TOKENFORGE_VERIFY_PROCESS_CLEANUP=PASS"
      echo "TOKENFORGE_VERIFY_CLEANUP_RESULT=PASS"
    fi
  else
    echo "TOKENFORGE_VERIFY_BLOCKING_PROCESS_COUNT=unknown"
    echo "TOKENFORGE_VERIFY_BLOCKING_PROCESSES=${output}"
    if [[ "${label}" == "post_cleanup" ]]; then
      echo "TOKENFORGE_VERIFY_PROCESS_CLEANUP=UNKNOWN"
      echo "TOKENFORGE_VERIFY_CLEANUP_RESULT=UNKNOWN"
    fi
  fi
  echo "TOKENFORGE_VERIFY_NEXT_COMMAND=$(manual_command)"
}

cleanup_processes() {
  echo "INFO [BuildPipeline][PROCESS_CLEANUP_BEGIN] action=${ACTION}"
  echo "INFO [UnityProcessCleanup] start action=${ACTION}"
  safe_kill_log_pids
  safe_kill_pattern "VBCSCompiler\.dll" "unity_roslyn_compiler_server"
  safe_kill_pattern "Unity\.Licensing\.Client" "unity_licensing_client"
  safe_kill_pattern "$(unity_batchmode_pattern)" "unity_${TOKENFORGE_UNITY_VERSION}_batchmode"
  if [[ "${TOKENFORGE_CLEAN_UNITY_HUB:-false}" == "true" ]]; then
    safe_kill_pattern "Unity Hub" "unity_hub"
  else
    echo "INFO [UnityProcessCleanup] skipped description=unity_hub reason=not_started_by_verification"
  fi
  sleep "${WAIT_SECONDS}"
  print_process_report "post_cleanup"
  echo "INFO [BuildPipeline][PROCESS_CLEANUP_RESULT] action=${ACTION}"
  echo "INFO [UnityProcessCleanup] done"
}

case "${ACTION}" in
  preflight|list)
    print_process_report "${ACTION}"
    ;;
  cleanup)
    cleanup_processes
    ;;
  *)
    echo "Usage: ${0} [preflight|list|cleanup]" >&2
    exit 64
    ;;
esac

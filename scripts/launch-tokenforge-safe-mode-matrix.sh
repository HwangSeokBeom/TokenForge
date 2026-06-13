#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

APP_BUNDLE_PATH="${APP_BUNDLE_PATH:-${REPO_ROOT}/UnityClient/builds/macOS/TokenForge.app}"
VERIFY_LOG_DIR="${VERIFY_LOG_DIR:-/private/tmp/tokenforge-safe-mode-matrix}"
WAIT_SECONDS="${WAIT_SECONDS:-60}"
PLAYER_LOG_PATH="${PLAYER_LOG_PATH:-${HOME}/Library/Logs/TokenForge/TokenForge/Player.log}"
RESULTS_TSV="${VERIFY_LOG_DIR}/safe-mode-matrix.tsv"
LOG_STREAM_PID=""

mkdir -p "${VERIFY_LOG_DIR}"

app_executable() {
  local plist="${APP_BUNDLE_PATH}/Contents/Info.plist"
  local executable_name="TokenForge"
  if [[ -f "${plist}" && -x /usr/libexec/PlistBuddy ]]; then
    executable_name="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "${plist}" 2>/dev/null || echo TokenForge)"
  fi
  printf '%s/Contents/MacOS/%s' "${APP_BUNDLE_PATH}" "${executable_name}"
}

file_mtime() {
  local path="$1"
  [[ -f "${path}" ]] || {
    echo 0
    return
  }
  stat -f '%m' "${path}" 2>/dev/null || echo 0
}

terminate_app() {
  local executable="$1"
  if command -v pgrep >/dev/null 2>&1; then
    local pids
    pids="$(pgrep -f "${executable}" 2>/dev/null || true)"
    if [[ -n "${pids}" ]]; then
      while IFS= read -r pid; do
        [[ -n "${pid}" ]] || continue
        kill -TERM "${pid}" >/dev/null 2>&1 || true
      done <<< "${pids}"
    fi
  fi
}

latest_files_after_epoch() {
  local launch_epoch="$1"
  local output_file="$2"
  : > "${output_file}"

  {
    find "${HOME}/Library/Logs/DiagnosticReports" -maxdepth 1 \( -name 'TokenForge*.ips' -o -name 'TokenForge*.crash' \) -type f -print0 2>/dev/null || true
    find "${APP_BUNDLE_PATH}/Contents" /tmp -name 'mono_crash.*.json' -type f -print0 2>/dev/null || true
  } | while IFS= read -r -d '' path; do
    local mtime
    mtime="$(file_mtime "${path}")"
    if [[ "${mtime}" =~ ^[0-9]+$ && "${mtime}" -ge "${launch_epoch}" ]]; then
      printf '%s\n' "${path}" >> "${output_file}"
    fi
  done
}

copy_player_log_if_fresh() {
  local launch_epoch="$1"
  local output_file="$2"
  : > "${output_file}"
  if [[ ! -f "${PLAYER_LOG_PATH}" ]]; then
    printf 'Player.log missing: %s\n' "${PLAYER_LOG_PATH}" > "${output_file}"
    return 1
  fi

  local mtime
  mtime="$(file_mtime "${PLAYER_LOG_PATH}")"
  if [[ ! "${mtime}" =~ ^[0-9]+$ || "${mtime}" -lt "${launch_epoch}" ]]; then
    printf 'Player.log not fresh for this launch: %s mtime=%s launchEpoch=%s\n' "${PLAYER_LOG_PATH}" "${mtime}" "${launch_epoch}" > "${output_file}"
    return 1
  fi

  cp "${PLAYER_LOG_PATH}" "${output_file}"
}

write_tail() {
  local input_file="$1"
  local output_file="$2"
  if [[ -f "${input_file}" ]]; then
    tail -100 "${input_file}" > "${output_file}" || true
  else
    : > "${output_file}"
  fi
}

run_case() {
  local name="$1"
  shift
  local executable="$1"
  shift
  local case_dir="${VERIFY_LOG_DIR}/${name}"
  local direct_log="${case_dir}/direct-launch.log"
  local log_stream_file="${case_dir}/log-stream.log"
  local player_log_copy="${case_dir}/Player.log"
  local player_log_tail="${case_dir}/Player.tail.log"
  local crash_files="${case_dir}/new-crash-files.txt"
  local pid_file="${case_dir}/tokenforge.pid"
  local command_file="${case_dir}/launch-command.txt"

  rm -rf "${case_dir}"
  mkdir -p "${case_dir}"
  : > "${direct_log}"
  : > "${log_stream_file}"
  : > "${pid_file}"

  local env_display="TOKENFORGE_VERIFY_RUNTIME=1"
  for env_pair in "$@"; do
    env_display="${env_display} ${env_pair}"
  done
  printf 'cd %q && %s %q -TokenForgeVerifyRuntime YES\n' \
    "$(dirname "${executable}")" "${env_display}" "${executable}" > "${command_file}"

  echo "INFO [SafeModeMatrix][BEGIN] case=${name} waitSeconds=${WAIT_SECONDS}"
  echo "INFO [SafeModeMatrix][COMMAND] $(cat "${command_file}")"

  log stream --style compact --predicate 'process CONTAINS "TokenForge"' --level debug >> "${log_stream_file}" 2>&1 &
  LOG_STREAM_PID=$!

  local launch_epoch
  launch_epoch="$(date +%s)"
  (
    cd "$(dirname "${executable}")"
    export TOKENFORGE_VERIFY_RUNTIME=1
    for env_pair in "$@"; do
      export "${env_pair}"
    done
    "${executable}" -TokenForgeVerifyRuntime YES &
    echo "$!" > "${pid_file}"
    wait "$(cat "${pid_file}")"
  ) >> "${direct_log}" 2>&1 &

  local elapsed=0
  while [[ "${elapsed}" -lt "${WAIT_SECONDS}" ]]; do
    local current_pid=""
    if [[ -s "${pid_file}" ]]; then
      current_pid="$(cat "${pid_file}")"
    fi
    if [[ -n "${current_pid}" ]] && ! kill -0 "${current_pid}" >/dev/null 2>&1; then
      break
    fi
    sleep 1
    elapsed=$((elapsed + 1))
  done

  if [[ -n "${LOG_STREAM_PID}" ]] && kill -0 "${LOG_STREAM_PID}" >/dev/null 2>&1; then
    kill "${LOG_STREAM_PID}" >/dev/null 2>&1 || true
    wait "${LOG_STREAM_PID}" >/dev/null 2>&1 || true
  fi
  LOG_STREAM_PID=""

  local app_pid=""
  if [[ -s "${pid_file}" ]]; then
    app_pid="$(cat "${pid_file}")"
  fi

  local survived="false"
  if [[ -n "${app_pid}" ]] && kill -0 "${app_pid}" >/dev/null 2>&1; then
    survived="true"
  fi

  local player_fresh="false"
  if copy_player_log_if_fresh "${launch_epoch}" "${player_log_copy}"; then
    player_fresh="true"
  fi
  write_tail "${player_log_copy}" "${player_log_tail}"

  latest_files_after_epoch "${launch_epoch}" "${crash_files}"
  local new_crash="false"
  if [[ -s "${crash_files}" ]]; then
    new_crash="true"
  fi

  local build_identity="false"
  local idle_reached="false"
  if grep -Fq "[BuildIdentity][RUNTIME_CODE_VERSION]" "${player_log_copy}" "${direct_log}" "${log_stream_file}" 2>/dev/null; then
    build_identity="true"
  fi
  if grep -Fq "[StartupDiagnostic][IDLE_REACHED]" "${player_log_copy}" "${direct_log}" "${log_stream_file}" 2>/dev/null; then
    idle_reached="true"
  fi

  local status="FAIL"
  if [[ "${survived}" == "true" && "${build_identity}" == "true" && "${idle_reached}" == "true" && "${new_crash}" == "false" ]]; then
    status="PASS"
  fi

  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
    "${name}" "${status}" "${survived}" "${build_identity}" "${idle_reached}" "${new_crash}" "${player_fresh}" "${command_file}" "${player_log_tail}" >> "${RESULTS_TSV}"

  echo "INFO [SafeModeMatrix][RESULT] case=${name} status=${status} survived=${survived} buildIdentity=${build_identity} idleReached=${idle_reached} newCrash=${new_crash} playerLogFresh=${player_fresh} dir=${case_dir}"

  terminate_app "${executable}"
  sleep 2
}

main() {
  local executable
  executable="$(app_executable)"
  if [[ ! -x "${executable}" ]]; then
    echo "TokenForge executable is missing or not executable: ${executable}" >&2
    exit 2
  fi

  : > "${RESULTS_TSV}"
  printf 'case\tstatus\tsurvived60s\tbuildIdentity\tidleReached\tnewCrash\tplayerLogFresh\tcommandFile\tplayerLogTail\n' >> "${RESULTS_TSV}"

  echo "INFO [SafeModeMatrix][APP] ${APP_BUNDLE_PATH}"
  echo "INFO [SafeModeMatrix][EXECUTABLE] ${executable}"
  echo "INFO [SafeModeMatrix][RESULTS] ${RESULTS_TSV}"
  run_case native_safe_mode "${executable}" TOKENFORGE_NATIVE_SAFE_MODE=1
  run_case disable_native_overlay "${executable}" TOKENFORGE_DISABLE_NATIVE_OVERLAY=1
  run_case disable_status_item "${executable}" TOKENFORGE_DISABLE_STATUS_ITEM=1
  run_case disable_native_dashboard "${executable}" TOKENFORGE_DISABLE_NATIVE_DASHBOARD=1
  run_case disable_context_menu "${executable}" TOKENFORGE_DISABLE_CONTEXT_MENU=1
  run_case disable_pixel_native_renderer "${executable}" TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER=1
  run_case disable_movement_timers "${executable}" TOKENFORGE_DISABLE_MOVEMENT_TIMERS=1
  run_case combined_safe_isolation "${executable}" \
    TOKENFORGE_DISABLE_NATIVE_OVERLAY=1 \
    TOKENFORGE_DISABLE_STATUS_ITEM=1 \
    TOKENFORGE_DISABLE_NATIVE_DASHBOARD=1 \
    TOKENFORGE_DISABLE_CONTEXT_MENU=1 \
    TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER=1 \
    TOKENFORGE_DISABLE_MOVEMENT_TIMERS=1

  echo "INFO [SafeModeMatrix][SUMMARY]"
  column -t -s $'\t' "${RESULTS_TSV}" 2>/dev/null || cat "${RESULTS_TSV}"
}

trap 'if [[ -n "${LOG_STREAM_PID}" ]] && kill -0 "${LOG_STREAM_PID}" >/dev/null 2>&1; then kill "${LOG_STREAM_PID}" >/dev/null 2>&1 || true; fi' EXIT
main "$@"

#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
CLEANUP_SCRIPT="${SCRIPT_DIR}/tokenforge-clean-unity-processes.sh"

UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity}"

VERIFY_ROOT="${VERIFY_ROOT:-/tmp/tokenforge-local-verify-all}"
LICENSE_LOG="${LICENSE_LOG:-${VERIFY_ROOT}/unity-license-smoke.log}"
EDITMODE_RESULTS="${EDITMODE_RESULTS:-${VERIFY_ROOT}/editmode-results.xml}"
EDITMODE_LOG="${EDITMODE_LOG:-${VERIFY_ROOT}/unity-editmode.log}"
PLAYMODE_RESULTS="${PLAYMODE_RESULTS:-${VERIFY_ROOT}/playmode-results.xml}"
PLAYMODE_LOG="${PLAYMODE_LOG:-${VERIFY_ROOT}/unity-playmode.log}"
BUILD_OUTPUT="${BUILD_OUTPUT:-/tmp/tokenforge-macos-build-bypass-codex/TokenForge.app}"
BUILD_LOG="${BUILD_LOG:-/tmp/tokenforge-macos-build-bypass-codex.log}"
VERIFY_APP_PATH="${VERIFY_APP_PATH:-/tmp/tokenforge-runtime-verify/TokenForge-verify.app}"
VERIFY_LOG_DIR="${VERIFY_LOG_DIR:-/tmp/tokenforge-runtime-verify}"
VERIFY_RUNTIME_WAIT_SECONDS="${VERIFY_RUNTIME_WAIT_SECONDS:-30}"
RUN_PLAYMODE="${RUN_PLAYMODE:-false}"
MANUAL_UI_VERIFIED="${MANUAL_UI_VERIFIED:-NO}"
TOKENFORGE_SKIP_FRESH_PHASES="${TOKENFORGE_SKIP_FRESH_PHASES:-false}"
TOKENFORGE_VERBOSE="${TOKENFORGE_VERBOSE:-0}"
SOURCE_FINGERPRINT_FILE="${VERIFY_ROOT}/source-fingerprint.sha256"

for arg in "$@"; do
  case "${arg}" in
    --verbose)
      TOKENFORGE_VERBOSE=1
      ;;
    --help|-h)
      echo "Usage: RUN_PLAYMODE=false scripts/tokenforge-local-verify-all.sh [--verbose]"
      exit 0
      ;;
  esac
done

mkdir -p "${VERIFY_ROOT}" "${VERIFY_LOG_DIR}"

cleanup_unity_verification_processes() {
  if [[ -x "${CLEANUP_SCRIPT}" ]]; then
    TOKENFORGE_UNITY_CLEANUP_LOGS="${LICENSE_LOG}:${EDITMODE_LOG}:${PLAYMODE_LOG}:${BUILD_LOG}" UNITY_PATH="${UNITY_PATH}" "${CLEANUP_SCRIPT}" cleanup || true
  fi
}

preflight_unity_verification_processes() {
  if [[ -x "${CLEANUP_SCRIPT}" ]]; then
    TOKENFORGE_UNITY_CLEANUP_LOGS="${LICENSE_LOG}:${EDITMODE_LOG}:${PLAYMODE_LOG}:${BUILD_LOG}" UNITY_PATH="${UNITY_PATH}" "${CLEANUP_SCRIPT}" preflight || true
  fi
}

trap cleanup_unity_verification_processes EXIT
trap 'cleanup_unity_verification_processes; exit 130' INT
trap 'cleanup_unity_verification_processes; exit 143' TERM

section() {
  printf '\n== %s ==\n' "$1"
}

failure_kind_for_label() {
  local label="$1"
  local exit_code="$2"
  case "${label}" in
    "Unity License Smoke")
      if [[ -s "${LICENSE_LOG}" ]] && grep -qE "error CS[0-9]+|Scripts have compiler errors" "${LICENSE_LOG}"; then
        echo "compiler"
      else
        echo "license"
      fi
      ;;
    *"EditMode"*) echo "editmode" ;;
    *"PlayMode"*) echo "playmode" ;;
    *"Player Rebuild"*|*"Build"*) echo "build" ;;
    *"Artifact Freshness"*) echo "artifact" ;;
    *"Runtime"*) echo "runtime" ;;
    *)
      if [[ "${exit_code}" -eq 7 ]]; then
        echo "manual"
      elif [[ "${exit_code}" -eq 124 ]]; then
        echo "process"
      else
        echo "process"
      fi
      ;;
  esac
}

print_failed_tests_from_xml() {
  local path="$1"
  [[ -s "${path}" ]] || return 0
  /usr/bin/python3 - "${path}" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
try:
    root = ET.parse(path).getroot()
except Exception as exc:
    print(f"TOKENFORGE_VERIFY_XML_PARSE_ERROR={exc}")
    raise SystemExit(0)

failed = [tc for tc in root.iter("test-case") if tc.attrib.get("result") == "Failed"]
if failed:
    print(f"TOKENFORGE_VERIFY_FAILED_TEST_COUNT={len(failed)}")
for tc in failed:
    print("TOKENFORGE_VERIFY_FAILED_TEST=" + (tc.attrib.get("fullname") or tc.attrib.get("name") or "unknown"))
    message = (tc.findtext("failure/message") or "").strip().replace("\n", " ")
    if message:
        print("TOKENFORGE_VERIFY_FAILED_TEST_MESSAGE=" + message[:500])
PY
}

emit_failure_report() {
  local label="$1"
  local exit_code="$2"
  local kind
  kind="$(failure_kind_for_label "${label}" "${exit_code}")"
	  section "Concise Failure Report"
	  echo "TOKENFORGE_VERIFY_RESULT=BLOCKED"
	  echo "TOKENFORGE_VERIFY_STATUS=BLOCKED"
	  echo "TOKENFORGE_VERIFY_FAILED_PHASE=${label}"
	  echo "TOKENFORGE_VERIFY_FAILURE_KIND=${kind}"
	  echo "TOKENFORGE_VERIFY_EXIT_CODE=${exit_code}"
  echo "TOKENFORGE_VERIFY_XML=${EDITMODE_RESULTS}"
  echo "TOKENFORGE_VERIFY_RUNTIME_LOG=${VERIFY_LOG_DIR}/tokenforge-log-stream.log"
  echo "TOKENFORGE_VERIFY_CRASH_LOG=$(ls -t "${VERIFY_LOG_DIR}"/mono_crash*.json "$HOME"/Library/Logs/DiagnosticReports/*TokenForge*.ips 2>/dev/null | head -1 || true)"
  echo "TOKENFORGE_VERIFY_LICENSE_LOG=${LICENSE_LOG}"
  echo "TOKENFORGE_VERIFY_EDITMODE_XML=${EDITMODE_RESULTS}"
  echo "TOKENFORGE_VERIFY_EDITMODE_LOG=${EDITMODE_LOG}"
  echo "TOKENFORGE_VERIFY_PLAYMODE_XML=${PLAYMODE_RESULTS}"
  echo "TOKENFORGE_VERIFY_PLAYMODE_LOG=${PLAYMODE_LOG}"
  echo "TOKENFORGE_VERIFY_BUILD_LOG=${BUILD_LOG}"
  echo "TOKENFORGE_VERIFY_RUNTIME_LOG_DIR=${VERIFY_LOG_DIR}"
  preflight_unity_verification_processes
  if [[ "${kind}" == "compiler" && -s "${LICENSE_LOG}" ]]; then
    grep -nE "error CS[0-9]+|Scripts have compiler errors|Assets/_Project|\\.cs\\(" "${LICENSE_LOG}" | tail -200 || true
  fi
  if [[ "${kind}" == "editmode" ]]; then
    print_failed_tests_from_xml "${EDITMODE_RESULTS}"
  fi
  if [[ "${kind}" == "playmode" ]]; then
    print_failed_tests_from_xml "${PLAYMODE_RESULTS}"
  fi
  echo "TOKENFORGE_VERIFY_NEXT_COMMAND=RUN_PLAYMODE=${RUN_PLAYMODE} scripts/tokenforge-local-verify-all.sh"
}

current_source_fingerprint() {
  (
    cd "${REPO_ROOT}" &&
    git ls-files -z |
      xargs -0 shasum -a 256 |
      shasum -a 256 |
      awk '{print $1}'
  )
}

source_fingerprint_matches() {
  [[ -s "${SOURCE_FINGERPRINT_FILE}" ]] || return 1
  local current
  current="$(current_source_fingerprint)"
  [[ "${current}" == "$(cat "${SOURCE_FINGERPRINT_FILE}")" ]]
}

record_source_fingerprint() {
  current_source_fingerprint > "${SOURCE_FINGERPRINT_FILE}"
}

should_skip_fresh_xml_phase() {
  local path="$1"
  [[ "${TOKENFORGE_SKIP_FRESH_PHASES}" == "true" ]] || return 1
  [[ -s "${path}" ]] || return 1
  source_fingerprint_matches
}

print_path_summary() {
  section "Paths"
  echo "Repository root: ${REPO_ROOT}"
  echo "Unity project path: ${UNITY_PROJECT_PATH}"
  echo "Unity executable: ${UNITY_PATH}"
  echo "License smoke log: ${LICENSE_LOG}"
  echo "EditMode XML: ${EDITMODE_RESULTS}"
  echo "EditMode log: ${EDITMODE_LOG}"
  echo "PlayMode enabled: ${RUN_PLAYMODE}"
  echo "PlayMode XML: ${PLAYMODE_RESULTS}"
  echo "PlayMode log: ${PLAYMODE_LOG}"
  echo "Skip fresh phases mode: ${TOKENFORGE_SKIP_FRESH_PHASES}"
  echo "Player build output: ${BUILD_OUTPUT}"
  echo "Player build log: ${BUILD_LOG}"
  echo "Runtime verify app: ${VERIFY_APP_PATH}"
  echo "Runtime log dir: ${VERIFY_LOG_DIR}"
  echo "Runtime log stream: ${VERIFY_LOG_DIR}/tokenforge-log-stream.log"
  echo "Runtime log show: ${VERIFY_LOG_DIR}/tokenforge-log-show.log"
	  echo "Runtime bundle hashes: ${VERIFY_LOG_DIR}/bundle-hashes.sha256"
  echo "Source fingerprint: ${SOURCE_FINGERPRINT_FILE}"
}

print_command() {
  local label="$1"
  shift

  echo "${label}:"
  printf ' %q' "$@"
  printf '\n'
}

run_command() {
  local label="$1"
  shift

  section "${label}"
  print_command "Command" "$@"
  case "${label}" in
    *"Unity"*|*"Player Rebuild"*|*"Build"*) preflight_unity_verification_processes ;;
  esac
  "$@"
  local exit_code=$?
  case "${label}" in
    *"Unity"*|*"Player Rebuild"*|*"Build"*) cleanup_unity_verification_processes ;;
  esac
  echo "Exit code: ${exit_code}"
  if [[ "${exit_code}" -ne 0 ]]; then
    echo "${label}: FAILED"
    emit_failure_report "${label}" "${exit_code}"
    exit "${exit_code}"
  fi
  echo "${label}: PASS"
}

run_env_command() {
  local label="$1"
  shift

  section "${label}"
  print_command "Command" env "$@"
  case "${label}" in
    *"Unity"*|*"Player Rebuild"*|*"Build"*) preflight_unity_verification_processes ;;
  esac
  env "$@"
  local exit_code=$?
  case "${label}" in
    *"Unity"*|*"Player Rebuild"*|*"Build"*) cleanup_unity_verification_processes ;;
  esac
  echo "Exit code: ${exit_code}"
  if [[ "${exit_code}" -ne 0 ]]; then
    echo "${label}: FAILED"
    emit_failure_report "${label}" "${exit_code}"
    exit "${exit_code}"
  fi
  echo "${label}: PASS"
}

require_file() {
  local label="$1"
  local path="$2"

  section "${label}"
  echo "Required file: ${path}"
  if [[ ! -s "${path}" ]]; then
    echo "${label}: FAILED"
    echo "Required file is missing or empty: ${path}"
    emit_failure_report "${label}" 1
    exit 1
  fi
  echo "${label}: PASS"
}

require_dir() {
  local label="$1"
  local path="$2"

  section "${label}"
  echo "Required directory: ${path}"
  if [[ ! -d "${path}" ]]; then
    echo "${label}: FAILED"
    echo "Required directory is missing: ${path}"
    emit_failure_report "${label}" 1
    exit 1
  fi
  echo "${label}: PASS"
}

parse_unity_xml() {
  local label="$1"
  local path="$2"

  section "${label}"
  echo "XML path: ${path}"
  if [[ ! -s "${path}" ]]; then
    echo "${label}: FAILED"
    echo "Unity XML is missing or empty. PASS is not claimed."
    emit_failure_report "${label}" 1
    exit 1
  fi

  /usr/bin/python3 - "${path}" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()
passed = int(root.attrib.get("passed", "0"))
failed = int(root.attrib.get("failed", "0"))
inconclusive = int(root.attrib.get("inconclusive", "0"))
skipped = int(root.attrib.get("skipped", "0"))
total = int(root.attrib.get("total", passed + failed + inconclusive + skipped))
print(f"Unity XML summary: total={total} passed={passed} failed={failed} inconclusive={inconclusive} skipped={skipped}")
if failed > 0:
    raise SystemExit(2)
PY
  local exit_code=$?
  echo "Exit code: ${exit_code}"
  if [[ "${exit_code}" -ne 0 ]]; then
    echo "${label}: FAILED"
    print_failed_tests_from_xml "${path}"
    emit_failure_report "${label}" "${exit_code}"
    exit "${exit_code}"
  fi
  echo "${label}: PASS"
}

run_runtime_verification() {
  section "Automated Runtime Verification"
  print_command "Command" env \
    "MANUAL_UI_VERIFIED=${MANUAL_UI_VERIFIED}" \
    "BUILD_OUTPUT=${BUILD_OUTPUT}" \
	    "VERIFY_APP_PATH=${VERIFY_APP_PATH}" \
	    "VERIFY_LOG_DIR=${VERIFY_LOG_DIR}" \
	    "VERIFY_RUNTIME_WAIT_SECONDS=${VERIFY_RUNTIME_WAIT_SECONDS}" \
	    "TOKENFORGE_VERBOSE=${TOKENFORGE_VERBOSE}" \
	    "${SCRIPT_DIR}/verify-tokenforge-runtime.sh"

  env \
    "MANUAL_UI_VERIFIED=${MANUAL_UI_VERIFIED}" \
    "BUILD_OUTPUT=${BUILD_OUTPUT}" \
	    "VERIFY_APP_PATH=${VERIFY_APP_PATH}" \
	    "VERIFY_LOG_DIR=${VERIFY_LOG_DIR}" \
	    "VERIFY_RUNTIME_WAIT_SECONDS=${VERIFY_RUNTIME_WAIT_SECONDS}" \
	    "TOKENFORGE_VERBOSE=${TOKENFORGE_VERBOSE}" \
	    "${SCRIPT_DIR}/verify-tokenforge-runtime.sh"
  local exit_code=$?
  echo "Exit code: ${exit_code}"
  echo "Runtime log stream: ${VERIFY_LOG_DIR}/tokenforge-log-stream.log"
  echo "Runtime log show: ${VERIFY_LOG_DIR}/tokenforge-log-show.log"
  echo "Runtime bundle hashes: ${VERIFY_LOG_DIR}/bundle-hashes.sha256"

  if [[ "${exit_code}" -eq 7 ]]; then
    echo "Automated runtime gates passed, but manual UI verification is still required."
    echo "MANUAL_UI_VERIFIED=YES is allowed only after you personally complete the UI checklist."
    echo "Final result: BLOCKED on manual UI verification."
    record_source_fingerprint
    emit_failure_report "Manual UI Verification" "${exit_code}"
    exit 7
  fi

  if [[ "${exit_code}" -ne 0 ]]; then
    echo "Automated runtime verification failed. PASS is not claimed."
    emit_failure_report "Automated Runtime Verification" "${exit_code}"
    exit "${exit_code}"
  fi

  echo "Automated runtime verification: PASS"
  record_source_fingerprint
}

print_path_summary
preflight_unity_verification_processes

section "Manual UI Verification Policy"
echo "Current MANUAL_UI_VERIFIED: ${MANUAL_UI_VERIFIED}"
echo "Leave MANUAL_UI_VERIFIED unset or NO until you personally complete the manual UI checklist."
echo "After manual UI verification, rerun this script with MANUAL_UI_VERIFIED=YES."
echo "This script never runs git commit or git push."
echo "TOKENFORGE_SKIP_FRESH_PHASES=${TOKENFORGE_SKIP_FRESH_PHASES} may reuse prior Unity XML only when the source fingerprint matches."
echo "TOKENFORGE_VERBOSE=${TOKENFORGE_VERBOSE}; pass --verbose or TOKENFORGE_VERBOSE=1 for full xattr/hash detail."

run_env_command "Unity License Smoke" \
  "LOG_FILE=${LICENSE_LOG}" \
  "UNITY_PATH=${UNITY_PATH}" \
  "${SCRIPT_DIR}/verify-unity-license-smoke.sh"

run_command "git diff --check" git diff --check

run_command "Native Symbol Verification" "${SCRIPT_DIR}/verify-native-symbols.sh"

run_command "DesktopCompanionOverlay clang Syntax Check" \
  xcrun clang++ \
  -x objective-c++ \
  -std=c++17 \
  -fsyntax-only \
  -isysroot "$(xcrun --sdk macosx --show-sdk-path)" \
  -mmacosx-version-min=10.15 \
  "${UNITY_PROJECT_PATH}/Assets/Plugins/macOS/DesktopCompanionOverlay.mm"

if should_skip_fresh_xml_phase "${EDITMODE_RESULTS}"; then
  section "Unity EditMode Tests"
  echo "Skipped because TOKENFORGE_SKIP_FRESH_PHASES=true and source fingerprint matches prior successful runtime verification."
else
  run_env_command "Unity EditMode Tests" \
    "UNITY_PATH=${UNITY_PATH}" \
    "RUN_EDITMODE=true" \
    "RUN_PLAYMODE=false" \
    "EDITMODE_RESULTS=${EDITMODE_RESULTS}" \
    "EDITMODE_LOG=${EDITMODE_LOG}" \
    "${SCRIPT_DIR}/verify-unity-tests.sh"
fi
parse_unity_xml "EditMode XML Freshness and Result Gate" "${EDITMODE_RESULTS}"

if [[ "${RUN_PLAYMODE}" == "true" ]]; then
  if should_skip_fresh_xml_phase "${PLAYMODE_RESULTS}"; then
    section "Unity PlayMode Tests"
    echo "Skipped because TOKENFORGE_SKIP_FRESH_PHASES=true and source fingerprint matches prior successful runtime verification."
  else
    run_env_command "Unity PlayMode Tests" \
      "UNITY_PATH=${UNITY_PATH}" \
      "RUN_EDITMODE=false" \
      "RUN_PLAYMODE=true" \
      "PLAYMODE_RESULTS=${PLAYMODE_RESULTS}" \
      "PLAYMODE_LOG=${PLAYMODE_LOG}" \
      "${SCRIPT_DIR}/verify-unity-tests.sh"
  fi
  parse_unity_xml "PlayMode XML Freshness and Result Gate" "${PLAYMODE_RESULTS}"
else
  section "Unity PlayMode Tests"
  echo "Skipped because RUN_PLAYMODE=${RUN_PLAYMODE}."
fi

section "Remove Previous Explicit Build Output"
print_command "Command" rm -rf "${BUILD_OUTPUT}"
rm -rf "${BUILD_OUTPUT}"
cleanup_exit_code=$?
echo "Exit code: ${cleanup_exit_code}"
if [[ "${cleanup_exit_code}" -ne 0 ]]; then
  echo "Remove Previous Explicit Build Output: FAILED"
  emit_failure_report "Remove Previous Explicit Build Output" "${cleanup_exit_code}"
  exit "${cleanup_exit_code}"
fi

run_env_command "macOS Player Rebuild" \
  "BUILD_OUTPUT=${BUILD_OUTPUT}" \
  "LOG_FILE=${BUILD_LOG}" \
  "CLEAN_BUILD=false" \
  "TOKENFORGE_VERBOSE=${TOKENFORGE_VERBOSE}" \
  "${SCRIPT_DIR}/build-macos-smoke.sh"
require_dir "Player Rebuild Output Gate" "${BUILD_OUTPUT}"
require_file "Player Executable Gate" "${BUILD_OUTPUT}/Contents/MacOS/TokenForge"

run_env_command "Artifact Freshness Verification" \
  "BUILD_OUTPUT=${BUILD_OUTPUT}" \
  "APP_BUNDLE_PATH=${BUILD_OUTPUT}" \
  "TOKENFORGE_VERBOSE=${TOKENFORGE_VERBOSE}" \
  "${SCRIPT_DIR}/verify-macos-build-artifacts.sh"

run_runtime_verification

section "Final Result"
echo "PASS"
echo "TOKENFORGE_VERIFY_STATUS=PASS"
echo "TOKENFORGE_VERIFY_FAILURE_KIND=none"
echo "TOKENFORGE_VERIFY_XML=${EDITMODE_RESULTS}"
echo "TOKENFORGE_VERIFY_RUNTIME_LOG=${VERIFY_LOG_DIR}/tokenforge-log-stream.log"
echo "TOKENFORGE_VERIFY_NEXT_COMMAND=MANUAL_UI_VERIFIED=YES RUN_PLAYMODE=${RUN_PLAYMODE} scripts/tokenforge-local-verify-all.sh"
echo "Manual UI verification flag accepted: ${MANUAL_UI_VERIFIED}"

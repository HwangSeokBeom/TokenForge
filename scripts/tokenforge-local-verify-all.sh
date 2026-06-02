#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

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

mkdir -p "${VERIFY_ROOT}" "${VERIFY_LOG_DIR}"

section() {
  printf '\n== %s ==\n' "$1"
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
  echo "Player build output: ${BUILD_OUTPUT}"
  echo "Player build log: ${BUILD_LOG}"
  echo "Runtime verify app: ${VERIFY_APP_PATH}"
  echo "Runtime log dir: ${VERIFY_LOG_DIR}"
  echo "Runtime log stream: ${VERIFY_LOG_DIR}/tokenforge-log-stream.log"
  echo "Runtime log show: ${VERIFY_LOG_DIR}/tokenforge-log-show.log"
  echo "Runtime bundle hashes: ${VERIFY_LOG_DIR}/bundle-hashes.sha256"
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
  "$@"
  local exit_code=$?
  echo "Exit code: ${exit_code}"
  if [[ "${exit_code}" -ne 0 ]]; then
    echo "${label}: FAILED"
    exit "${exit_code}"
  fi
  echo "${label}: PASS"
}

run_env_command() {
  local label="$1"
  shift

  section "${label}"
  print_command "Command" env "$@"
  env "$@"
  local exit_code=$?
  echo "Exit code: ${exit_code}"
  if [[ "${exit_code}" -ne 0 ]]; then
    echo "${label}: FAILED"
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
    "${SCRIPT_DIR}/verify-tokenforge-runtime.sh"

  env \
    "MANUAL_UI_VERIFIED=${MANUAL_UI_VERIFIED}" \
    "BUILD_OUTPUT=${BUILD_OUTPUT}" \
    "VERIFY_APP_PATH=${VERIFY_APP_PATH}" \
    "VERIFY_LOG_DIR=${VERIFY_LOG_DIR}" \
    "VERIFY_RUNTIME_WAIT_SECONDS=${VERIFY_RUNTIME_WAIT_SECONDS}" \
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
    exit 7
  fi

  if [[ "${exit_code}" -ne 0 ]]; then
    echo "Automated runtime verification failed. PASS is not claimed."
    exit "${exit_code}"
  fi

  echo "Automated runtime verification: PASS"
}

print_path_summary

section "Manual UI Verification Policy"
echo "Current MANUAL_UI_VERIFIED: ${MANUAL_UI_VERIFIED}"
echo "Leave MANUAL_UI_VERIFIED unset or NO until you personally complete the manual UI checklist."
echo "After manual UI verification, rerun this script with MANUAL_UI_VERIFIED=YES."
echo "This script never runs git commit or git push."

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

run_env_command "Unity EditMode Tests" \
  "UNITY_PATH=${UNITY_PATH}" \
  "RUN_EDITMODE=true" \
  "RUN_PLAYMODE=false" \
  "EDITMODE_RESULTS=${EDITMODE_RESULTS}" \
  "EDITMODE_LOG=${EDITMODE_LOG}" \
  "${SCRIPT_DIR}/verify-unity-tests.sh"
parse_unity_xml "EditMode XML Freshness and Result Gate" "${EDITMODE_RESULTS}"

if [[ "${RUN_PLAYMODE}" == "true" ]]; then
  run_env_command "Unity PlayMode Tests" \
    "UNITY_PATH=${UNITY_PATH}" \
    "RUN_EDITMODE=false" \
    "RUN_PLAYMODE=true" \
    "PLAYMODE_RESULTS=${PLAYMODE_RESULTS}" \
    "PLAYMODE_LOG=${PLAYMODE_LOG}" \
    "${SCRIPT_DIR}/verify-unity-tests.sh"
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
  exit "${cleanup_exit_code}"
fi

run_env_command "macOS Player Rebuild" \
  "BUILD_OUTPUT=${BUILD_OUTPUT}" \
  "LOG_FILE=${BUILD_LOG}" \
  "CLEAN_BUILD=false" \
  "${SCRIPT_DIR}/build-macos-smoke.sh"
require_dir "Player Rebuild Output Gate" "${BUILD_OUTPUT}"
require_file "Player Executable Gate" "${BUILD_OUTPUT}/Contents/MacOS/TokenForge"

run_env_command "Artifact Freshness Verification" \
  "BUILD_OUTPUT=${BUILD_OUTPUT}" \
  "APP_BUNDLE_PATH=${BUILD_OUTPUT}" \
  "${SCRIPT_DIR}/verify-macos-build-artifacts.sh"

run_runtime_verification

section "Final Result"
echo "PASS"
echo "Manual UI verification flag accepted: ${MANUAL_UI_VERIFIED}"

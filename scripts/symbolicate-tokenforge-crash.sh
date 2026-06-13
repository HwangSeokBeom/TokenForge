#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"

APP_BUNDLE_PATH="${APP_BUNDLE_PATH:-${REPO_ROOT}/UnityClient/builds/macOS/TokenForge.app}"
CRASH_SEARCH_ROOT="${CRASH_SEARCH_ROOT:-${HOME}/Library/Logs/DiagnosticReports}"
MONO_CRASH_SEARCH_ROOT="${MONO_CRASH_SEARCH_ROOT:-${APP_BUNDLE_PATH}/Contents}"
DYLIB_PATH="${DYLIB_PATH:-${APP_BUNDLE_PATH}/Contents/PlugIns/libDesktopCompanionOverlay.dylib}"
UNITY_PLAYER_PATH="${UNITY_PLAYER_PATH:-${APP_BUNDLE_PATH}/Contents/Frameworks/UnityPlayer.dylib}"
DSYM_PATH="${DSYM_PATH:-${APP_BUNDLE_PATH}.dSYM}"
CRASH_FILE="${CRASH_FILE:-}"

latest_file() {
  local root="$1"
  local pattern="$2"
  [[ -d "${root}" ]] || return 1
  find "${root}" -name "${pattern}" -type f -print0 2>/dev/null \
    | xargs -0 stat -f '%m %N' 2>/dev/null \
    | sort -nr \
    | head -1 \
    | cut -d' ' -f2-
}

ips_body_file() {
  local crash_file="$1"
  local body_file="$2"
  tail -n +2 "${crash_file}" > "${body_file}"
}

image_name_for_index() {
  local body_file="$1"
  local index="$2"
  jq -r --argjson index "${index}" '.usedImages[$index].name // "unknown"' "${body_file}"
}

image_path_for_index() {
  local body_file="$1"
  local index="$2"
  jq -r --argjson index "${index}" '.usedImages[$index].path // "unknown"' "${body_file}"
}

image_base_for_index() {
  local body_file="$1"
  local index="$2"
  jq -r --argjson index "${index}" '.usedImages[$index].base // empty' "${body_file}"
}

hex_address() {
  printf '0x%x' "$1"
}

symbolicate_frame() {
  local image_name="$1"
  local image_path="$2"
  local base_decimal="$3"
  local offset_decimal="$4"

  [[ "${base_decimal}" =~ ^[0-9]+$ && "${offset_decimal}" =~ ^[0-9]+$ ]] || return 0

  local load_address frame_address binary_path
  load_address="$(hex_address "${base_decimal}")"
  frame_address="$(hex_address "$((base_decimal + offset_decimal))")"

  case "${image_name}" in
    UnityPlayer.dylib)
      binary_path="${UNITY_PLAYER_PATH}"
      ;;
    libDesktopCompanionOverlay.dylib)
      binary_path="${DYLIB_PATH}"
      ;;
    TokenForge)
      binary_path="${APP_BUNDLE_PATH}/Contents/MacOS/TokenForge"
      ;;
    *)
      return 0
      ;;
  esac

  if [[ ! -f "${binary_path}" ]]; then
    printf '  symbolication: unavailable binaryMissing=%s\n' "${binary_path}"
    return 0
  fi

  printf '  symbolication: atos -arch arm64 -o %q -l %s %s => ' "${binary_path}" "${load_address}" "${frame_address}"
  atos -arch arm64 -o "${binary_path}" -l "${load_address}" "${frame_address}" 2>/dev/null || true
}

print_ips_summary() {
  local crash_file="$1"
  local body_file
  body_file="$(mktemp "${TMPDIR:-/tmp}/tokenforge-ips-body.XXXXXX")"
  ips_body_file "${crash_file}" "${body_file}"

  echo "INFO [CrashSymbolication][IPS_FILE] ${crash_file}"
  echo "INFO [CrashSymbolication][HEADER]"
  head -1 "${crash_file}" | jq '{app_name,timestamp,app_version,build_version,bundleID,os_version,slice_uuid,bug_type}'

  echo "INFO [CrashSymbolication][FACTS]"
  jq '{
    procPath,
    bundleInfo,
    exception,
    termination,
    asi,
    faultingThread,
    crashedThreadName: (.threads[.faultingThread].name // null),
    crashedThreadQueue: (.threads[.faultingThread].queue // null)
  }' "${body_file}"

  echo "INFO [CrashSymbolication][CRASHED_THREAD_BACKTRACE]"
  local frame_count index image_index image_name image_path image_base image_offset symbol symbol_location
  frame_count="$(jq '.threads[.faultingThread].frames | length' "${body_file}")"
  for ((index = 0; index < frame_count; index++)); do
    image_index="$(jq -r --argjson index "${index}" '.threads[.faultingThread].frames[$index].imageIndex // empty' "${body_file}")"
    image_name="unknown"
    image_path="unknown"
    image_base=""
    if [[ "${image_index}" =~ ^[0-9]+$ ]]; then
      image_name="$(image_name_for_index "${body_file}" "${image_index}")"
      image_path="$(image_path_for_index "${body_file}" "${image_index}")"
      image_base="$(image_base_for_index "${body_file}" "${image_index}")"
    fi
    image_offset="$(jq -r --argjson index "${index}" '.threads[.faultingThread].frames[$index].imageOffset // empty' "${body_file}")"
    symbol="$(jq -r --argjson index "${index}" '.threads[.faultingThread].frames[$index].symbol // "<unsymbolicated>"' "${body_file}")"
    symbol_location="$(jq -r --argjson index "${index}" '.threads[.faultingThread].frames[$index].symbolLocation // 0' "${body_file}")"
    printf '#%02d %-34s offset=%s symbol=%s + %s path=%s\n' "${index}" "${image_name}" "${image_offset}" "${symbol}" "${symbol_location}" "${image_path}"
    symbolicate_frame "${image_name}" "${image_path}" "${image_base}" "${image_offset}"
  done

  echo "INFO [CrashSymbolication][IMAGE_PRESENCE]"
  jq -r '
    def hasImage($needle): any(.usedImages[]?; ((.name // "") | contains($needle)) or ((.path // "") | contains($needle)));
    "libDesktopCompanionOverlay.dylib=" + (hasImage("libDesktopCompanionOverlay.dylib")|tostring),
    "UnityPlayer.dylib=" + (hasImage("UnityPlayer.dylib")|tostring),
    "Mono=" + (hasImage("mono")|tostring),
    "AppKit=" + (hasImage("AppKit")|tostring),
    "CoreFoundation=" + (hasImage("CoreFoundation")|tostring),
    "ObjectiveCRuntime=" + (hasImage("libobjc")|tostring),
    "LaunchServices=" + (hasImage("LaunchServices")|tostring)
  ' "${body_file}"

  echo "INFO [CrashSymbolication][BINARY_IMAGES]"
  jq -r '.usedImages[]? | [.name // "unknown", .base // "unknown", .size // "unknown", .uuid // "unknown", .path // "unknown"] | @tsv' "${body_file}"

  rm -f "${body_file}"
}

echo "INFO [CrashSymbolication][BEGIN] appBundle=${APP_BUNDLE_PATH}"

MONO_CRASH_FILE="$(latest_file "${MONO_CRASH_SEARCH_ROOT}" 'mono_crash.*.json' || true)"
if [[ -z "${MONO_CRASH_FILE}" ]]; then
  MONO_CRASH_FILE="$(latest_file /tmp 'mono_crash.*.json' || true)"
fi

if [[ -z "${CRASH_FILE}" ]]; then
  CRASH_FILE="$(latest_file "${CRASH_SEARCH_ROOT}" 'TokenForge*.ips' || true)"
fi
if [[ -z "${CRASH_FILE}" ]]; then
  CRASH_FILE="$(latest_file "${CRASH_SEARCH_ROOT}" 'TokenForge*.crash' || true)"
fi

echo "INFO [CrashSymbolication][CRASH_FILE] mono=${MONO_CRASH_FILE:-missing} macos=${CRASH_FILE:-missing}"
echo "INFO [CrashSymbolication][DYLIB_PATH] ${DYLIB_PATH}"
echo "INFO [CrashSymbolication][UNITY_PLAYER_PATH] ${UNITY_PLAYER_PATH}"
if [[ -d "${DSYM_PATH}" ]]; then
  echo "INFO [CrashSymbolication][DSYM_PATH] ${DSYM_PATH}"
else
  echo "INFO [CrashSymbolication][DSYM_PATH] missing:${DSYM_PATH}"
fi

if [[ -f "${DYLIB_PATH}" ]]; then
  echo "INFO [CrashSymbolication][DYLIB_HASH] $(shasum -a 256 "${DYLIB_PATH}" | awk '{print $1}')"
  dwarfdump --uuid "${DYLIB_PATH}" || true
else
  echo "INFO [CrashSymbolication][DYLIB] unavailable reason=dylib_missing"
fi

if [[ -f "${UNITY_PLAYER_PATH}" ]]; then
  dwarfdump --uuid "${UNITY_PLAYER_PATH}" || true
else
  echo "INFO [CrashSymbolication][UNITY_PLAYER] unavailable reason=unity_player_missing"
fi

if [[ -n "${CRASH_FILE}" && "${CRASH_FILE}" == *.ips ]]; then
  print_ips_summary "${CRASH_FILE}"
elif [[ -n "${CRASH_FILE}" ]]; then
  echo "INFO [CrashSymbolication][CRASH_TEXT]"
  sed -n '1,220p' "${CRASH_FILE}"
elif [[ -n "${MONO_CRASH_FILE}" ]]; then
  echo "INFO [CrashSymbolication][RESULT] mono crash JSON found but macOS DiagnosticReports crash file missing."
else
  echo "INFO [CrashSymbolication][RESULT] no crash files found under ${MONO_CRASH_SEARCH_ROOT}, /tmp, or ${CRASH_SEARCH_ROOT}."
fi

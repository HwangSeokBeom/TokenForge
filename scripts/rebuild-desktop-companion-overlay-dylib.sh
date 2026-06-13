#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
SOURCE_MM="${SOURCE_MM:-${UNITY_PROJECT_PATH}/Assets/Plugins/macOS/DesktopCompanionOverlay.mm}"
OUTPUT_DYLIB="${OUTPUT_DYLIB:-${UNITY_PROJECT_PATH}/Assets/Plugins/macOS/libDesktopCompanionOverlay.dylib}"
SDK_PATH="${SDK_PATH:-$(xcrun --sdk macosx --show-sdk-path)}"
MACOS_MIN_VERSION="${MACOS_MIN_VERSION:-10.15}"
ARCHS="${ARCHS:-arm64}"
RUNTIME_MARKER="${RUNTIME_MARKER:-tokenforge_runtime_fix_20260609_230131}"

if [[ ! -f "${SOURCE_MM}" ]]; then
  echo "DesktopCompanionOverlay source not found: ${SOURCE_MM}" >&2
  exit 1
fi

mkdir -p "$(dirname "${OUTPUT_DYLIB}")"
tmp_output="$(mktemp "${TMPDIR:-/tmp}/libDesktopCompanionOverlay.XXXXXX.dylib")"
trap 'rm -f "${tmp_output}"' EXIT

compile_command=(
  xcrun clang++
  -dynamiclib
  -x objective-c++
  -std=c++17
  -isysroot "${SDK_PATH}"
  -mmacosx-version-min="${MACOS_MIN_VERSION}"
  -fvisibility=default
  -install_name "@rpath/libDesktopCompanionOverlay.dylib"
)

for arch in ${ARCHS}; do
  compile_command+=(-arch "${arch}")
done

compile_command+=(
  "${SOURCE_MM}"
  -framework Cocoa
  -o "${tmp_output}"
)

echo "INFO [NativeBuild][BEGIN] source=${SOURCE_MM} output=${OUTPUT_DYLIB}"
echo "INFO [NativeBuild][SOURCE_TIMESTAMP] $(stat -f '%Sm' "${SOURCE_MM}")"
echo "INFO [NativeBuild][COMPILED_MARKER] expectedRuntimeMarker=${RUNTIME_MARKER}"
echo "Rebuilding DesktopCompanionOverlay dylib"
echo "Source: ${SOURCE_MM}"
echo "Output: ${OUTPUT_DYLIB}"
echo "Runtime marker: ${RUNTIME_MARKER}"
echo "Architectures: ${ARCHS}"
"${compile_command[@]}"

if ! strings "${tmp_output}" | grep -Fq "${RUNTIME_MARKER}"; then
  echo "Rebuilt dylib does not contain runtime marker: ${RUNTIME_MARKER}" >&2
  exit 2
fi

mv "${tmp_output}" "${OUTPUT_DYLIB}"
touch "${OUTPUT_DYLIB}"

echo "DesktopCompanionOverlay dylib rebuilt"
echo "Dylib path: ${OUTPUT_DYLIB}"
echo "INFO [NativeBuild][DYLIB_OUTPUT_TIMESTAMP] $(stat -f '%Sm' "${OUTPUT_DYLIB}")"
echo "INFO [NativeBuild][DYLIB_HASH] $(shasum -a 256 "${OUTPUT_DYLIB}" | awk '{print $1}')"
echo "INFO [NativeBuild][COMPILED_MARKER] ${RUNTIME_MARKER}"

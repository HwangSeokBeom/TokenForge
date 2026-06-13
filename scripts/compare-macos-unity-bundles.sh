#!/usr/bin/env bash
set -euo pipefail

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" || "$#" -ne 2 ]]; then
  echo "Usage: scripts/compare-macos-unity-bundles.sh /path/to/TokenForge.app /path/to/MinimalUnity.app"
  exit 2
fi

LEFT_APP="$1"
RIGHT_APP="$2"
WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/tokenforge-bundle-diff.XXXXXX")"

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

section() {
  printf '\n== %s ==\n' "$1"
}

row() {
  printf '%-36s | %-10s | %s\n' "$1" "$2" "$3"
}

plist_value() {
  local plist="$1"
  local key="$2"
  /usr/libexec/PlistBuddy -c "Print :${key}" "${plist}" 2>/dev/null || true
}

app_executable() {
  local app="$1"
  local executable_name
  executable_name="$(plist_value "${app}/Contents/Info.plist" CFBundleExecutable)"
  if [[ -z "${executable_name}" ]]; then
    executable_name="$(basename "${app}" .app)"
  fi
  printf '%s/Contents/MacOS/%s' "${app}" "${executable_name}"
}

normalize_tree() {
  local app="$1"
  local output="$2"
  (
    cd "${app}"
    find . -maxdepth 4 -print | LC_ALL=C sort | sed 's#^\./##'
  ) > "${output}"
}

permission_report() {
  local app="$1"
  local output="$2"
  (
    cd "${app}"
    find Contents/MacOS -maxdepth 1 -print0 2>/dev/null \
      | while IFS= read -r -d '' path; do
          stat -f '%Sp %N' "${path}" 2>/dev/null || true
        done
  ) | LC_ALL=C sort > "${output}"
}

xattr_report() {
  local app="$1"
  local output="$2"
  xattr -lr "${app}" 2>/dev/null \
    | sed "s#${app}#<APP>#g" \
    | LC_ALL=C sort > "${output}" || true
}

codesign_report() {
  local app="$1"
  local output="$2"
  {
    codesign -dv --verbose=4 "${app}" 2>&1 || true
    codesign --verify --deep --strict --verbose=4 "${app}" 2>&1 || true
    if command -v spctl >/dev/null 2>&1; then
      spctl --assess --type execute --verbose=4 "${app}" 2>&1 || true
    fi
  } | sed "s#${app}#<APP>#g" > "${output}"
}

otool_l_report() {
  local binary="$1"
  local output="$2"
  if [[ -f "${binary}" ]]; then
    otool -L "${binary}" 2>&1 | sed "s#${binary}#<BINARY>#g" > "${output}" || true
  else
    echo "missing: ${binary}" > "${output}"
  fi
}

rpath_report() {
  local binary="$1"
  local output="$2"
  if [[ -f "${binary}" ]]; then
    otool -l "${binary}" 2>/dev/null | awk '
      /cmd LC_RPATH/ {show=1}
      show {print}
      show && /path / {show=0}
    ' > "${output}" || true
  else
    echo "missing: ${binary}" > "${output}"
  fi
}

plist_report() {
  local app="$1"
  local output="$2"
  local plist="${app}/Contents/Info.plist"
  if [[ -f "${plist}" ]]; then
    plutil -p "${plist}" > "${output}"
  else
    echo "missing: ${plist}" > "${output}"
  fi
}

print_diff() {
  local title="$1"
  local left="$2"
  local right="$3"
  section "${title}"
  if diff -u "${left}" "${right}"; then
    row "${title}" "MATCH" "no diff"
  else
    row "${title}" "DIFF" "differences shown above"
  fi
}

if [[ ! -d "${LEFT_APP}" ]]; then
  row "left app" "FAIL" "missing: ${LEFT_APP}"
  exit 2
fi

if [[ ! -d "${RIGHT_APP}" ]]; then
  row "right app" "FAIL" "missing: ${RIGHT_APP}"
  exit 2
fi

LEFT_EXECUTABLE="$(app_executable "${LEFT_APP}")"
RIGHT_EXECUTABLE="$(app_executable "${RIGHT_APP}")"
LEFT_UNITY_PLAYER="${LEFT_APP}/Contents/Frameworks/UnityPlayer.dylib"
RIGHT_UNITY_PLAYER="${RIGHT_APP}/Contents/Frameworks/UnityPlayer.dylib"

section "Bundle Inputs"
row "left app" "INFO" "${LEFT_APP}"
row "right app" "INFO" "${RIGHT_APP}"
row "left executable" "INFO" "${LEFT_EXECUTABLE}"
row "right executable" "INFO" "${RIGHT_EXECUTABLE}"

plist_report "${LEFT_APP}" "${WORK_DIR}/left.plist"
plist_report "${RIGHT_APP}" "${WORK_DIR}/right.plist"
normalize_tree "${LEFT_APP}" "${WORK_DIR}/left.tree"
normalize_tree "${RIGHT_APP}" "${WORK_DIR}/right.tree"
permission_report "${LEFT_APP}" "${WORK_DIR}/left.perms"
permission_report "${RIGHT_APP}" "${WORK_DIR}/right.perms"
xattr_report "${LEFT_APP}" "${WORK_DIR}/left.xattrs"
xattr_report "${RIGHT_APP}" "${WORK_DIR}/right.xattrs"
codesign_report "${LEFT_APP}" "${WORK_DIR}/left.codesign"
codesign_report "${RIGHT_APP}" "${WORK_DIR}/right.codesign"
otool_l_report "${LEFT_EXECUTABLE}" "${WORK_DIR}/left.exec.otool"
otool_l_report "${RIGHT_EXECUTABLE}" "${WORK_DIR}/right.exec.otool"
rpath_report "${LEFT_EXECUTABLE}" "${WORK_DIR}/left.exec.rpath"
rpath_report "${RIGHT_EXECUTABLE}" "${WORK_DIR}/right.exec.rpath"
otool_l_report "${LEFT_UNITY_PLAYER}" "${WORK_DIR}/left.unity.otool"
otool_l_report "${RIGHT_UNITY_PLAYER}" "${WORK_DIR}/right.unity.otool"
rpath_report "${LEFT_UNITY_PLAYER}" "${WORK_DIR}/left.unity.rpath"
rpath_report "${RIGHT_UNITY_PLAYER}" "${WORK_DIR}/right.unity.rpath"

print_diff "Info.plist Diff" "${WORK_DIR}/left.plist" "${WORK_DIR}/right.plist"
print_diff "Bundle Tree Diff" "${WORK_DIR}/left.tree" "${WORK_DIR}/right.tree"
print_diff "Executable Permission Diff" "${WORK_DIR}/left.perms" "${WORK_DIR}/right.perms"
print_diff "Xattr Summary Diff" "${WORK_DIR}/left.xattrs" "${WORK_DIR}/right.xattrs"
print_diff "Codesign Summary Diff" "${WORK_DIR}/left.codesign" "${WORK_DIR}/right.codesign"
print_diff "Main Executable otool -L Diff" "${WORK_DIR}/left.exec.otool" "${WORK_DIR}/right.exec.otool"
print_diff "Main Executable LC_RPATH Diff" "${WORK_DIR}/left.exec.rpath" "${WORK_DIR}/right.exec.rpath"
print_diff "UnityPlayer otool -L Diff" "${WORK_DIR}/left.unity.otool" "${WORK_DIR}/right.unity.otool"
print_diff "UnityPlayer LC_RPATH Diff" "${WORK_DIR}/left.unity.rpath" "${WORK_DIR}/right.unity.rpath"

section "UnityPlayer Identity"
if [[ -f "${LEFT_UNITY_PLAYER}" && -f "${RIGHT_UNITY_PLAYER}" ]]; then
  LEFT_UNITY_HASH="$(shasum -a 256 "${LEFT_UNITY_PLAYER}" | awk '{print $1}')"
  RIGHT_UNITY_HASH="$(shasum -a 256 "${RIGHT_UNITY_PLAYER}" | awk '{print $1}')"
  row "left UnityPlayer sha256" "INFO" "${LEFT_UNITY_HASH}"
  row "right UnityPlayer sha256" "INFO" "${RIGHT_UNITY_HASH}"
  if [[ "${LEFT_UNITY_HASH}" == "${RIGHT_UNITY_HASH}" ]]; then
    row "UnityPlayer identity" "MATCH" "same file hash"
  else
    row "UnityPlayer identity" "DIFF" "different file hash"
  fi
else
  row "UnityPlayer identity" "WARN" "one or both UnityPlayer.dylib files are missing"
fi

section "Classification Hints"
if ! diff -q "${WORK_DIR}/left.plist" "${WORK_DIR}/right.plist" >/dev/null; then
  row "plist" "CHECK" "registration-sensitive metadata differs"
fi
if ! diff -q "${WORK_DIR}/left.perms" "${WORK_DIR}/right.perms" >/dev/null; then
  row "permissions" "CHECK" "Contents/MacOS permissions differ"
fi
if [[ -s "${WORK_DIR}/left.xattrs" || -s "${WORK_DIR}/right.xattrs" ]]; then
  row "xattrs" "CHECK" "extended attributes are present in at least one bundle"
fi
if ! diff -q "${WORK_DIR}/left.codesign" "${WORK_DIR}/right.codesign" >/dev/null; then
  row "codesign" "CHECK" "signing metadata or verification output differs"
fi
if ! diff -q "${WORK_DIR}/left.exec.otool" "${WORK_DIR}/right.exec.otool" >/dev/null \
  || ! diff -q "${WORK_DIR}/left.exec.rpath" "${WORK_DIR}/right.exec.rpath" >/dev/null; then
  row "main Mach-O" "CHECK" "load commands or rpaths differ"
fi
if ! diff -q "${WORK_DIR}/left.unity.otool" "${WORK_DIR}/right.unity.otool" >/dev/null \
  || ! diff -q "${WORK_DIR}/left.unity.rpath" "${WORK_DIR}/right.unity.rpath" >/dev/null; then
  row "UnityPlayer Mach-O" "CHECK" "UnityPlayer load commands or rpaths differ"
fi

row "next step" "INFO" "interpret hints only with verifier and fresh crash output"

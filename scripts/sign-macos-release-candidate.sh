#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

EXPECTED_PRODUCT_NAME="${TOKENFORGE_EXPECTED_PRODUCT_NAME:-TokenForge}"
EXPECTED_BUNDLE_ID="${TOKENFORGE_MACOS_BUNDLE_ID:-com.tokenforge.client}"
EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
ENTITLEMENTS_PATH="${TOKENFORGE_MACOS_ENTITLEMENTS:-${REPO_ROOT}/BuildSupport/macOS/TokenForge.entitlements}"
DEVELOPER_ID_IDENTITY="${DEVELOPER_ID_APPLICATION:-${TOKENFORGE_MACOS_SIGN_IDENTITY:-}}"
SIGN=false
STRICT=false

for arg in "$@"; do
  case "${arg}" in
    --sign)
      SIGN=true
      STRICT=true
      ;;
    --strict)
      STRICT=true
      ;;
    --help|-h)
      echo "Usage: scripts/sign-macos-release-candidate.sh [--sign] [--strict]"
      echo "Dry-run inspection is default. --sign requires DEVELOPER_ID_APPLICATION."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

status="SIGNING_DRY_RUN_READY"
verify_dir=""
work_app=""
bundle_id=""
product_name=""
short_version=""
build_number=""
signing_identity_type="unknown"
hardened_runtime="unknown"
timestamp_status="unknown"
entitlements_status="unknown"
codesign_status="unknown"
developer_id_identity_available="false"
signed_with_developer_id="false"
can_sign="false"
release_candidate_sha256=""
error_category=""
safe_failure_reason=""

safe_path() {
  local value="$1"
  if [[ "${value}" == /tmp/tokenforge-release/* || "${value}" == /tmp/tokenforge-macos-build/* || "${value}" == /tmp/tokenforge-signing-rc.* ]]; then
    echo "${value}"
  else
    echo "controlled-path"
  fi
}

json_escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

sha256_for_package() {
  if [[ -f "${PACKAGE_PATH}" ]]; then
    shasum -a 256 "${PACKAGE_PATH}" | awk '{print $1}'
  fi
}

masked_identity_display() {
  local identity="$1"
  if [[ -z "${identity}" ]]; then
    echo ""
    return
  fi
  if [[ "${identity}" =~ ^Developer\ ID\ Application:\ .*\ \([A-Z0-9]{10}\)$ ]]; then
    echo "${identity}"
  elif [[ "${identity}" == Developer\ ID\ Application:* ]]; then
    echo "Developer ID Application: configured"
  else
    echo "configured-signing-identity"
  fi
}

finish() {
  local exit_code="$1"
  local signing_summary="${RELEASE_DIR}/signing-status-summary.json"
  mkdir -p "${RELEASE_DIR}"
  release_candidate_sha256="$(sha256_for_package)"
  cat >"${signing_summary}" <<JSON
{
  "schemaVersion": 2,
  "status": "$(json_escape "${status}")",
  "signingIdentityType": "$(json_escape "${signing_identity_type}")",
  "developerIdIdentityAvailable": ${developer_id_identity_available},
  "signedWithDeveloperId": ${signed_with_developer_id},
  "hardenedRuntime": "$(json_escape "${hardened_runtime}")",
  "timestamp": "$(json_escape "${timestamp_status}")",
  "entitlementsStatus": "$(json_escape "${entitlements_status}")",
  "appBundleId": "$(json_escape "${bundle_id}")",
  "appVersion": "$(json_escape "${short_version}")",
  "appBuild": "$(json_escape "${build_number}")",
  "releaseCandidatePath": "$(json_escape "$(safe_path "${PACKAGE_PATH}")")",
  "releaseCandidateSha256": "$(json_escape "${release_candidate_sha256}")",
  "existingSigningStatus": "$(json_escape "${codesign_status}")",
  "canSign": ${can_sign},
  "signingIdentityDisplay": "$(json_escape "$(masked_identity_display "${DEVELOPER_ID_IDENTITY}")")",
  "errorCategory": "$(json_escape "${error_category}")",
  "safeFailureReason": "$(json_escape "${safe_failure_reason}")"
}
JSON
  if grep -Eiq 'APPLE_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|TOKENFORGE_MACOS_SIGN_IDENTITY|/Users/|/private/|tokenforge-approved-locations\.local\.json|raw[ _-]?sync[ _-]?payload|BEGIN PRIVATE KEY|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|token[[:space:]]*[:=]' "${signing_summary}"; then
    rm -f "${signing_summary}"
    echo "Signing summary contains unsafe content." >&2
    exit 1
  fi

  echo "STATUS: ${status}"
  echo "APP_BUNDLE_PATH: $(safe_path "${APP_PATH}")"
  echo "RELEASE_CANDIDATE_PATH: $(safe_path "${PACKAGE_PATH}")"
  echo "RELEASE_CANDIDATE_SHA256: ${release_candidate_sha256:-unknown}"
  echo "BUNDLE_IDENTIFIER: ${bundle_id:-unknown}"
  echo "VERSION: ${short_version:-unknown}"
  echo "BUILD: ${build_number:-unknown}"
  echo "EXISTING_SIGNING_STATUS: ${codesign_status}"
  echo "SIGNING_IDENTITY_TYPE: ${signing_identity_type}"
  echo "SIGNED_WITH_DEVELOPER_ID: ${signed_with_developer_id}"
  echo "HARDENED_RUNTIME: ${hardened_runtime}"
  echo "TIMESTAMP: ${timestamp_status}"
  echo "ENTITLEMENTS_STATUS: ${entitlements_status}"
  echo "DEVELOPER_ID_IDENTITY_AVAILABLE: ${developer_id_identity_available}"
  echo "CAN_SIGN: ${can_sign}"
  if [[ -n "${error_category}" ]]; then
    echo "ERROR_CATEGORY: ${error_category}"
    echo "SAFE_FAILURE_REASON: ${safe_failure_reason}"
  fi
  echo "SIGNING_SUMMARY_PATH: $(safe_path "${signing_summary}")"
  if [[ -n "${verify_dir}" ]]; then
    rm -rf "${verify_dir}"
  fi
  exit "${exit_code}"
}

block() {
  status="$1"
  error_category="$2"
  safe_failure_reason="$3"
  finish 1
}

plist_value() {
  /usr/libexec/PlistBuddy -c "Print :$1" "$2" 2>/dev/null || true
}

inspect_codesign() {
  local app="$1"
  local signing_info runtime_info entitlements_info
  if ! command -v codesign >/dev/null 2>&1; then
    codesign_status="codesignUnavailable"
    signing_identity_type="unknown"
    hardened_runtime="unknown"
    timestamp_status="unknown"
    return
  fi

  if codesign --verify --deep --strict "${app}" >/dev/null 2>&1; then
    codesign_status="verified"
  else
    codesign_status="notVerified"
  fi

  signing_info="$(codesign -dv "${app}" 2>&1 || true)"
  if grep -q 'Authority=Developer ID Application' <<<"${signing_info}"; then
    signing_identity_type="developerId"
    signed_with_developer_id="true"
  elif grep -q 'Signature=adhoc' <<<"${signing_info}" || ! grep -q 'Authority=' <<<"${signing_info}"; then
    signing_identity_type="adHoc"
    signed_with_developer_id="false"
  else
    signing_identity_type="unknown"
    signed_with_developer_id="false"
  fi

  runtime_info="$(codesign -dv --verbose=4 "${app}" 2>&1 || true)"
  if grep -qi 'runtime' <<<"${runtime_info}"; then
    hardened_runtime="enabled"
  else
    hardened_runtime="disabled"
  fi

  if grep -q 'Timestamp=' <<<"${runtime_info}"; then
    timestamp_status="present"
  else
    timestamp_status="notPresent"
  fi

  entitlements_info="$(codesign -d --entitlements :- "${app}" 2>/dev/null || true)"
  if grep -q '<dict>' <<<"${entitlements_info}"; then
    entitlements_status="present"
  elif [[ -f "${ENTITLEMENTS_PATH}" ]]; then
    entitlements_status="configuredNotDetected"
  else
    entitlements_status="notConfigured"
  fi
}

check_privacy_scan() {
  if [[ ! -f "${PACKAGE_PATH}" ]]; then
    return 1
  fi

  local listing
  listing="$(zipinfo -1 "${PACKAGE_PATH}")"
  if grep -Eiq 'tokenforge-approved-locations\.local\.json|tokenforge-sync-retry-queue\.local\.json|tokenforge-sync-tombstones\.local\.json|tokenforge-sync-conflicts\.local\.json|tokenforge-sync-conflict-audit\.local\.json|tokenforge-sync-local-state\.local\.json|tokenforge-session|tokenforge-token|raw[ _-]?sync[ _-]?payload|retry[ _-]?backup|recovery[ _-]?sync[ _-]?state|\.env$|\.log$|APPLE_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|TEAM_ID|credentials' <<<"${listing}"; then
    return 1
  fi
}

if [[ ! -f "${PACKAGE_PATH}" && ! -d "${APP_PATH}" ]]; then
  block "SIGNING_BLOCKED_MISSING_APP" "packageMissing" "Release candidate package or app bundle is missing."
fi

if [[ -f "${PACKAGE_PATH}" ]]; then
  check_privacy_scan || block "SIGNING_BLOCKED_PRIVACY_SCAN_FAILED" "privacyScanFailed" "Release candidate package contains disallowed local-only or sensitive files."
  verify_dir="$(mktemp -d /tmp/tokenforge-signing-rc.XXXXXX)"
  if ! unzip -q "${PACKAGE_PATH}" -d "${verify_dir}"; then
    block "SIGNING_BLOCKED_METADATA_INVALID" "metadataInvalid" "Release candidate package could not be inspected safely."
  fi
  work_app="${verify_dir}/TokenForge.app"
else
  work_app="${APP_PATH}"
fi

INFO_PLIST="${work_app}/Contents/Info.plist"
if [[ ! -d "${work_app}" || ! -f "${INFO_PLIST}" ]]; then
  block "SIGNING_BLOCKED_MISSING_APP" "packageMissing" "Release candidate app bundle is missing from the package."
fi

bundle_id="$(plist_value CFBundleIdentifier "${INFO_PLIST}")"
product_name="$(plist_value CFBundleDisplayName "${INFO_PLIST}")"
if [[ -z "${product_name}" ]]; then
  product_name="$(plist_value CFBundleName "${INFO_PLIST}")"
fi
short_version="$(plist_value CFBundleShortVersionString "${INFO_PLIST}")"
build_number="$(plist_value CFBundleVersion "${INFO_PLIST}")"

if [[ "${bundle_id}" != "${EXPECTED_BUNDLE_ID}" || "${product_name}" != "${EXPECTED_PRODUCT_NAME}" || "${short_version}" != "${EXPECTED_VERSION}" || "${build_number}" != "${EXPECTED_BUILD_NUMBER}" ]]; then
  block "SIGNING_BLOCKED_METADATA_INVALID" "metadataInvalid" "Release candidate metadata does not match the expected release values."
fi

if [[ -f "${ENTITLEMENTS_PATH}" ]]; then
  if /usr/bin/plutil -lint "${ENTITLEMENTS_PATH}" >/dev/null 2>&1; then
    entitlements_status="configured"
  else
    block "SIGNING_BLOCKED_ENTITLEMENTS_INVALID" "entitlementsInvalid" "Configured entitlements file is invalid."
  fi
else
  entitlements_status="notConfigured"
fi

inspect_codesign "${work_app}"

if [[ -n "${DEVELOPER_ID_IDENTITY}" ]] && command -v security >/dev/null 2>&1 && security find-identity -p codesigning -v 2>/dev/null | grep -Fq "${DEVELOPER_ID_IDENTITY}"; then
  developer_id_identity_available="true"
  can_sign="true"
fi

if [[ "${SIGN}" != "true" ]]; then
  if [[ "${developer_id_identity_available}" == "true" ]]; then
    status="SIGNING_DRY_RUN_READY"
  else
    status="SIGNING_READY_BUT_IDENTITY_MISSING"
    error_category="missingDeveloperIdIdentity"
    safe_failure_reason="Developer ID identity is not available on this machine; dry-run inspection completed."
  fi
  if [[ "${STRICT}" == "true" && "${developer_id_identity_available}" != "true" ]]; then
    finish 1
  fi
  finish 0
fi

if [[ "${developer_id_identity_available}" != "true" ]]; then
  status="SIGNING_READY_BUT_IDENTITY_MISSING"
  error_category="missingDeveloperIdIdentity"
  safe_failure_reason="Developer ID identity is required for credentialed signing."
  finish 1
fi

if ! command -v codesign >/dev/null 2>&1; then
  block "SIGNING_BLOCKED_METADATA_INVALID" "codesignFailed" "codesign is unavailable on this release machine."
fi

codesign_args=(--force --deep --options runtime --timestamp --identifier "${EXPECTED_BUNDLE_ID}" --sign "${DEVELOPER_ID_IDENTITY}")
if [[ -f "${ENTITLEMENTS_PATH}" ]]; then
  codesign_args+=(--entitlements "${ENTITLEMENTS_PATH}")
fi

if ! codesign "${codesign_args[@]}" "${work_app}" >/dev/null 2>&1; then
  block "SIGNING_BLOCKED_CODESIGN_FAILED" "codesignFailed" "Developer ID signing failed."
fi
if ! codesign --verify --deep --strict "${work_app}" >/dev/null 2>&1; then
  block "SIGNING_BLOCKED_VERIFICATION_FAILED" "verificationFailed" "Signed app verification failed."
fi
codesign -d --entitlements :- "${work_app}" >/dev/null 2>&1 || true

if command -v spctl >/dev/null 2>&1; then
  spctl --assess --type execute --verbose "${work_app}" >/dev/null 2>&1 || true
fi

if [[ -f "${PACKAGE_PATH}" ]]; then
  rm -f "${PACKAGE_PATH}"
  if ! ditto -c -k --sequesterRsrc --keepParent "${work_app}" "${PACKAGE_PATH}"; then
    block "SIGNING_BLOCKED_VERIFICATION_FAILED" "verificationFailed" "Signed app could not be repackaged."
  fi
else
  mkdir -p "$(dirname "${PACKAGE_PATH}")"
  if ! ditto -c -k --sequesterRsrc --keepParent "${work_app}" "${PACKAGE_PATH}"; then
    block "SIGNING_BLOCKED_VERIFICATION_FAILED" "verificationFailed" "Signed app could not be packaged."
  fi
fi

check_privacy_scan || block "SIGNING_BLOCKED_PRIVACY_SCAN_FAILED" "privacyScanFailed" "Signed package failed the privacy scan."
inspect_codesign "${work_app}"
if [[ "${signing_identity_type}" != "developerId" || "${hardened_runtime}" != "enabled" ]]; then
  block "SIGNING_BLOCKED_VERIFICATION_FAILED" "verificationFailed" "Signed app did not retain Developer ID signing and hardened runtime."
fi
status="SIGNING_SUCCEEDED"
finish 0

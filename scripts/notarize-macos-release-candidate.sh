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
SUBMIT=false

for arg in "$@"; do
  case "${arg}" in
    --submit)
      SUBMIT=true
      ;;
    --help|-h)
      echo "Usage: scripts/notarize-macos-release-candidate.sh [--submit]"
      echo "Dry-run readiness is default. --submit requires Developer ID signing and Apple notary credentials."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

status="NOTARIZATION_NOT_ATTEMPTED"
notarization_status="NOTARIZATION_NOT_ATTEMPTED"
submitted="false"
submission_id=""
notarization_started_at=""
notarization_finished_at=""
stapling_status="notAttempted"
spctl_status="notAttempted"
signing_identity_type="unknown"
hardened_runtime="unknown"
entitlements_status="unknown"
credentials_present="false"
release_candidate_sha256=""
error_category=""
safe_failure_reason=""
verify_dir=""

safe_path() {
  local value="$1"
  if [[ "${value}" == /tmp/tokenforge-release/* || "${value}" == /tmp/tokenforge-macos-build/* || "${value}" == /tmp/tokenforge-notary-readiness.* ]]; then
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

finish() {
  local exit_code="$1"
  local summary_path="${RELEASE_DIR}/notarization-status-summary.json"
  mkdir -p "${RELEASE_DIR}"
  release_candidate_sha256="$(sha256_for_package)"
  if [[ -z "${notarization_finished_at}" && ( "${submitted}" == "true" || "${exit_code}" != "0" ) ]]; then
    notarization_finished_at="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  fi
  cat >"${summary_path}" <<JSON
{
  "schemaVersion": 2,
  "status": "$(json_escape "${status}")",
  "notarizationStatus": "$(json_escape "${notarization_status}")",
  "submitted": ${submitted},
  "submissionId": "$(json_escape "${submission_id}")",
  "notarizationStartedAt": "$(json_escape "${notarization_started_at}")",
  "notarizationFinishedAt": "$(json_escape "${notarization_finished_at}")",
  "staplingStatus": "$(json_escape "${stapling_status}")",
  "spctlStatus": "$(json_escape "${spctl_status}")",
  "signedIdentityType": "$(json_escape "${signing_identity_type}")",
  "signingIdentityType": "$(json_escape "${signing_identity_type}")",
  "hardenedRuntime": "$(json_escape "${hardened_runtime}")",
  "entitlementsStatus": "$(json_escape "${entitlements_status}")",
  "releaseCandidatePath": "$(json_escape "$(safe_path "${PACKAGE_PATH}")")",
  "releaseCandidateSha256": "$(json_escape "${release_candidate_sha256}")",
  "appleCredentialsPresent": ${credentials_present},
  "errorCategory": "$(json_escape "${error_category}")",
  "safeFailureReason": "$(json_escape "${safe_failure_reason}")"
}
JSON
  if grep -Eiq 'APPLE_ID|APPLE_TEAM_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|/Users/|/private/|tokenforge-approved-locations\.local\.json|raw[ _-]?sync[ _-]?payload|BEGIN PRIVATE KEY|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|token[[:space:]]*[:=]|notarytool submit' "${summary_path}"; then
    rm -f "${summary_path}"
    echo "Notarization summary contains unsafe content." >&2
    exit 1
  fi

  echo "STATUS: ${status}"
  echo "NOTARIZATION_STATUS: ${notarization_status}"
  echo "SUBMITTED: ${submitted}"
  if [[ -n "${submission_id}" ]]; then
    echo "SUBMISSION_ID: ${submission_id}"
  fi
  echo "STAPLING_STATUS: ${stapling_status}"
  echo "SPCTL_STATUS: ${spctl_status}"
  echo "PACKAGE_PATH: $(safe_path "${PACKAGE_PATH}")"
  echo "RELEASE_CANDIDATE_SHA256: ${release_candidate_sha256:-unknown}"
  echo "APP_BUNDLE_PATH: $(safe_path "${APP_PATH}")"
  echo "SIGNING_IDENTITY_TYPE: ${signing_identity_type}"
  echo "HARDENED_RUNTIME: ${hardened_runtime}"
  echo "ENTITLEMENTS_STATUS: ${entitlements_status}"
  echo "APPLE_CREDENTIALS_PRESENT: ${credentials_present}"
  if [[ -n "${error_category}" ]]; then
    echo "ERROR_CATEGORY: ${error_category}"
    echo "SAFE_FAILURE_REASON: ${safe_failure_reason}"
  fi
  echo "NOTARIZATION_SUMMARY_PATH: $(safe_path "${summary_path}")"
  if [[ -n "${verify_dir}" ]]; then
    rm -rf "${verify_dir}"
  fi
  exit "${exit_code}"
}

block() {
  status="$1"
  notarization_status="$1"
  error_category="$2"
  safe_failure_reason="$3"
  finish 1
}

plist_value() {
  /usr/libexec/PlistBuddy -c "Print :$1" "$2" 2>/dev/null || true
}

json_field() {
  local file="$1"
  local field="$2"
  /usr/bin/python3 -c 'import json,sys; data=json.load(open(sys.argv[1])); print(data.get(sys.argv[2],""))' "${file}" "${field}" 2>/dev/null || true
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

inspect_codesign() {
  local app="$1"
  local signing_info runtime_info entitlements_info
  if ! command -v codesign >/dev/null 2>&1; then
    signing_identity_type="unknown"
    hardened_runtime="unknown"
    return 1
  fi

  if ! codesign --verify --deep --strict "${app}" >/dev/null 2>&1; then
    return 1
  fi

  signing_info="$(codesign -dv "${app}" 2>&1 || true)"
  if grep -q 'Authority=Developer ID Application' <<<"${signing_info}"; then
    signing_identity_type="developerId"
  elif grep -q 'Signature=adhoc' <<<"${signing_info}" || ! grep -q 'Authority=' <<<"${signing_info}"; then
    signing_identity_type="adHoc"
  else
    signing_identity_type="unknown"
  fi

  runtime_info="$(codesign -dv --verbose=4 "${app}" 2>&1 || true)"
  if grep -qi 'runtime' <<<"${runtime_info}"; then
    hardened_runtime="enabled"
  else
    hardened_runtime="disabled"
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

if [[ ! -f "${PACKAGE_PATH}" ]]; then
  block "NOTARIZATION_NOT_ATTEMPTED" "NOTARIZATION_NOT_ATTEMPTED" "packageMissing" "Release candidate package is missing."
fi

check_privacy_scan || block "NOTARIZATION_FAILED" "NOTARIZATION_FAILED" "metadataInvalid" "Release candidate package failed privacy inspection."

verify_dir="$(mktemp -d /tmp/tokenforge-notary-readiness.XXXXXX)"
if ! unzip -q "${PACKAGE_PATH}" -d "${verify_dir}"; then
  block "NOTARIZATION_FAILED" "NOTARIZATION_FAILED" "metadataInvalid" "Release candidate package could not be inspected safely."
fi
EXTRACTED_APP="${verify_dir}/TokenForge.app"
INFO_PLIST="${EXTRACTED_APP}/Contents/Info.plist"

if [[ ! -d "${EXTRACTED_APP}" || ! -f "${INFO_PLIST}" ]]; then
  block "NOTARIZATION_FAILED" "NOTARIZATION_FAILED" "packageMissing" "Release candidate app bundle is missing from the package."
fi

bundle_id="$(plist_value CFBundleIdentifier "${INFO_PLIST}")"
product_name="$(plist_value CFBundleDisplayName "${INFO_PLIST}")"
if [[ -z "${product_name}" ]]; then
  product_name="$(plist_value CFBundleName "${INFO_PLIST}")"
fi
short_version="$(plist_value CFBundleShortVersionString "${INFO_PLIST}")"
build_number="$(plist_value CFBundleVersion "${INFO_PLIST}")"

if [[ "${bundle_id}" != "${EXPECTED_BUNDLE_ID}" || "${product_name}" != "${EXPECTED_PRODUCT_NAME}" || "${short_version}" != "${EXPECTED_VERSION}" || "${build_number}" != "${EXPECTED_BUILD_NUMBER}" ]]; then
  block "NOTARIZATION_FAILED" "NOTARIZATION_FAILED" "metadataInvalid" "Release candidate metadata does not match the expected release values."
fi

inspect_codesign "${EXTRACTED_APP}" || block "NOTARIZATION_FAILED" "NOTARIZATION_FAILED" "metadataInvalid" "Release candidate signing could not be verified."

if [[ -n "${APPLE_ID:-}" && -n "${APPLE_TEAM_ID:-}" && -n "${APPLE_APP_SPECIFIC_PASSWORD:-}" ]]; then
  credentials_present="true"
fi

if [[ "${SUBMIT}" != "true" ]]; then
  if [[ "${signing_identity_type}" == "adHoc" ]]; then
    echo "NOTE: NOTARIZATION_BLOCKED_ADHOC_SIGNED"
    echo "NOTE: ad-hoc signed builds are not acceptable for distribution notarization."
    error_category="adhocSigned"
    safe_failure_reason="Dry-run found an ad-hoc signed build; Developer ID signing is required for final distribution."
  fi

  if [[ "${credentials_present}" == "true" && "${signing_identity_type}" == "developerId" ]]; then
    status="NOTARIZATION_NOT_ATTEMPTED"
    notarization_status="NOTARIZATION_NOT_ATTEMPTED"
  else
    status="NOTARIZATION_READY_BUT_CREDENTIALS_MISSING"
    notarization_status="NOTARIZATION_READY_BUT_CREDENTIALS_MISSING"
    if [[ -z "${error_category}" ]]; then
      error_category="missingCredentials"
      safe_failure_reason="Apple notarization credentials are required for submit mode."
    fi
  fi
  finish 0
fi

if [[ "${signing_identity_type}" != "developerId" ]]; then
  status="NOTARIZATION_BLOCKED_ADHOC_SIGNED"
  notarization_status="NOTARIZATION_BLOCKED_ADHOC_SIGNED"
  error_category="adhocSigned"
  safe_failure_reason="Submit mode requires a Developer ID signed package."
  finish 1
fi

if [[ "${credentials_present}" != "true" ]]; then
  status="NOTARIZATION_READY_BUT_CREDENTIALS_MISSING"
  notarization_status="NOTARIZATION_READY_BUT_CREDENTIALS_MISSING"
  error_category="missingCredentials"
  safe_failure_reason="APPLE_ID, APPLE_TEAM_ID, and APPLE_APP_SPECIFIC_PASSWORD are required for submit mode."
  finish 1
fi

if ! xcrun notarytool --help >/dev/null 2>&1; then
  block "NOTARIZATION_FAILED" "NOTARIZATION_FAILED" "notarytoolUnavailable" "Apple notarytool is unavailable on this release machine."
fi

notary_output="$(mktemp /tmp/tokenforge-notary-submit.XXXXXX.json)"
status="NOTARIZATION_SUBMITTED"
notarization_status="NOTARIZATION_SUBMITTED"
submitted="true"
notarization_started_at="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
if ! xcrun notarytool submit "${PACKAGE_PATH}" \
  --apple-id "${APPLE_ID}" \
  --team-id "${APPLE_TEAM_ID}" \
  --password "${APPLE_APP_SPECIFIC_PASSWORD}" \
  --output-format json \
  --wait >"${notary_output}" 2>/dev/null; then
  rm -f "${notary_output}"
  status="NOTARIZATION_FAILED"
  notarization_status="NOTARIZATION_FAILED"
  error_category="submitFailed"
  safe_failure_reason="notarytool submit failed; raw logs were not stored."
  finish 1
fi

submission_id="$(json_field "${notary_output}" id)"
notary_status="$(json_field "${notary_output}" status)"
rm -f "${notary_output}"
notarization_finished_at="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
if [[ "${notary_status}" != "Accepted" ]]; then
  status="NOTARIZATION_FAILED"
  notarization_status="NOTARIZATION_FAILED"
  error_category="notarizationRejected"
  safe_failure_reason="Apple notarization did not return Accepted; raw logs were not stored."
  finish 1
fi

notarization_status="NOTARIZATION_SUCCEEDED"

if ! xcrun stapler staple "${EXTRACTED_APP}" >/dev/null 2>&1; then
  status="STAPLE_FAILED"
  stapling_status="STAPLE_FAILED"
  error_category="stapleFailed"
  safe_failure_reason="Stapling failed after notarization succeeded."
  finish 1
fi

if ! xcrun stapler validate "${EXTRACTED_APP}" >/dev/null 2>&1; then
  status="STAPLE_FAILED"
  stapling_status="STAPLE_FAILED"
  error_category="stapleFailed"
  safe_failure_reason="Stapling validation failed after stapling."
  finish 1
fi
stapling_status="STAPLE_SUCCEEDED"

if ! ditto -c -k --sequesterRsrc --keepParent "${EXTRACTED_APP}" "${PACKAGE_PATH}"; then
  status="STAPLE_FAILED"
  stapling_status="STAPLE_FAILED"
  error_category="stapleFailed"
  safe_failure_reason="Stapled app could not be repackaged."
  finish 1
fi

if command -v spctl >/dev/null 2>&1; then
  if spctl --assess --type execute --verbose=4 "${EXTRACTED_APP}" >/dev/null 2>&1; then
    spctl_status="SPCTL_SUCCEEDED"
  else
    status="SPCTL_FAILED"
    spctl_status="SPCTL_FAILED"
    error_category="spctlFailed"
    safe_failure_reason="spctl assessment failed after notarization and stapling."
    finish 1
  fi
else
  spctl_status="notAvailable"
fi

status="NOTARIZATION_SUCCEEDED"
finish 0

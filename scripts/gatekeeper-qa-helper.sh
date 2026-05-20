#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
JSON_OUTPUT=false
INIT_STATUS=false
VALIDATE_STATUS=false
SET_STATUS_ARGS=()
SET_STATUS_ARGS_COUNT=0
NOTES=""

allowed_status_regex='^(notStarted|passed|failed|blocked|skippedWithReason)$'
qa_items=(cleanInstall firstLaunch moveToApplications quarantineAssessment localStateLocation safeSyncPanelLaunch confirmationModalCancel confirmationModalTypedPhrase conflictAuditLocalOnly retryTombstoneLocalOnly approvedLocationsLocalOnly privacyScan packageScan)

for arg in "$@"; do
  case "${arg}" in
    --json)
      JSON_OUTPUT=true
      ;;
    --init-status)
      INIT_STATUS=true
      ;;
    --validate-status)
      VALIDATE_STATUS=true
      ;;
    --set-status=*)
      SET_STATUS_ARGS[${SET_STATUS_ARGS_COUNT}]="${arg#--set-status=}"
      SET_STATUS_ARGS_COUNT=$((SET_STATUS_ARGS_COUNT + 1))
      ;;
    --notes=*)
      NOTES="${arg#--notes=}"
      ;;
    --help|-h)
      echo "Usage: scripts/gatekeeper-qa-helper.sh [--json] [--init-status] [--validate-status] [--set-status=item=status] [--notes=safe-note]"
      echo "Inspects release artifacts for QA. It does not disable Gatekeeper, remove quarantine, or inspect approved-location contents."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

safe_path() {
  local value="$1"
  if [[ "${value}" == /tmp/tokenforge-release/* || "${value}" == /tmp/tokenforge-macos-build/* ]]; then
    echo "${value}"
  else
    echo "controlled-path"
  fi
}

json_escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

safe_note() {
  local value="$1"
  if grep -Eiq '/Users/|/private/|tokenforge-approved-locations\.local\.json|raw[ _-]?sync[ _-]?payload|APPLE_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|BEGIN PRIVATE KEY|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|token[[:space:]]*[:=]|\{\"|Exception:|stack trace| at .*\:[0-9]+' <<<"${value}"; then
    return 1
  fi
}

write_status_file() {
  local status_path="${EVIDENCE_DIR}/gatekeeper-qa-status.json"
  local overall="notStarted"
  mkdir -p "${EVIDENCE_DIR}"

  local statuses=()
  for item in "${qa_items[@]}"; do
    statuses+=("notStarted")
  done

  local pair_index=0
  while [[ "${pair_index}" -lt "${SET_STATUS_ARGS_COUNT}" ]]; do
    local pair="${SET_STATUS_ARGS[${pair_index}]}"
    local item="${pair%%=*}"
    local status="${pair#*=}"
    local known=false
    local item_index=0
    local found_index=-1
    for allowed_item in "${qa_items[@]}"; do
      if [[ "${item}" == "${allowed_item}" ]]; then
        known=true
        found_index="${item_index}"
      fi
      item_index=$((item_index + 1))
    done
    if [[ "${known}" != "true" || ! "${status}" =~ ${allowed_status_regex} ]]; then
      echo "Invalid QA status item or value." >&2
      exit 1
    fi
    statuses["${found_index}"]="${status}"
    pair_index=$((pair_index + 1))
  done

  if [[ -n "${NOTES}" ]]; then
    safe_note "${NOTES}" || {
      echo "QA notes contain unsafe content." >&2
      exit 1
    }
  fi

  if printf '%s\n' "${statuses[@]}" | grep -qx 'failed'; then
    overall="failed"
  elif printf '%s\n' "${statuses[@]}" | grep -qx 'blocked'; then
    overall="blocked"
  elif ! printf '%s\n' "${statuses[@]}" | grep -qx 'notStarted' && printf '%s\n' "${statuses[@]}" | grep -Eqx 'passed|skippedWithReason'; then
    overall="passed"
  fi

  {
    echo "{"
    echo "  \"schemaVersion\": 1,"
    echo "  \"overallStatus\": \"$(json_escape "${overall}")\","
    echo "  \"items\": {"
    local index=0
    local total="${#qa_items[@]}"
    for item in "${qa_items[@]}"; do
      index=$((index + 1))
      local comma=","
      if [[ "${index}" -eq "${total}" ]]; then
        comma=""
      fi
      echo "    \"${item}\": \"$(json_escape "${statuses[$((index - 1))]}")\"${comma}"
    done
    echo "  },"
    echo "  \"notes\": \"$(json_escape "${NOTES}")\""
    echo "}"
  } >"${status_path}"

  if grep -Eiq '/Users/|/private/|APPLE_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|BEGIN PRIVATE KEY|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|token[[:space:]]*[:=]|\{\"|stack trace' "${status_path}"; then
    rm -f "${status_path}"
    echo "QA status file contains unsafe content." >&2
    exit 1
  fi

  echo "QA status path: $(safe_path "${status_path}")"
}

validate_status_file() {
  local status_path="${EVIDENCE_DIR}/gatekeeper-qa-status.json"
  if [[ ! -f "${status_path}" ]]; then
    echo "QA status file not found: $(safe_path "${status_path}")" >&2
    exit 1
  fi
  if grep -Eiq '/Users/|/private/|APPLE_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|BEGIN PRIVATE KEY|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|token[[:space:]]*[:=]|\{\"|stack trace|xattr -d com\.apple\.quarantine|spctl[[:space:]]+--master-disable|prompt:|response:|raw server body' "${status_path}"; then
    echo "QA status file contains unsafe content." >&2
    exit 1
  fi
  for status in $(grep -Eo '"(notStarted|passed|failed|blocked|skippedWithReason)"' "${status_path}" | tr -d '"'); do
    case "${status}" in
      notStarted|passed|failed|blocked|skippedWithReason) ;;
      *) echo "QA status contains invalid enum." >&2; exit 1 ;;
    esac
  done
  echo "QA status validation: passed"
}

if [[ "${INIT_STATUS}" == "true" || "${SET_STATUS_ARGS_COUNT}" -gt 0 ]]; then
  write_status_file
fi

if [[ "${VALIDATE_STATUS}" == "true" ]]; then
  validate_status_file
fi

package_exists="false"
package_local_only_absence="notRun"
codesign_status="notRun"
spctl_status="notRun"
stapling_status="notRun"
quarantine_status="notRun"
bundle_identifier="unknown"
short_version="unknown"
build_number="unknown"

if [[ -f "${PACKAGE_PATH}" ]]; then
  package_exists="true"
  listing="$(zipinfo -1 "${PACKAGE_PATH}")"
  if grep -Eiq 'tokenforge-approved-locations\.local\.json|tokenforge-sync-retry-queue\.local\.json|tokenforge-sync-tombstones\.local\.json|tokenforge-sync-conflicts\.local\.json|tokenforge-sync-conflict-audit\.local\.json|tokenforge-sync-local-state\.local\.json|tokenforge-session|tokenforge-token|raw[ _-]?sync[ _-]?payload|retry[ _-]?backup|recovery[ _-]?sync[ _-]?state|\.env$|\.log$|credentials' <<<"${listing}"; then
    package_local_only_absence="failed"
  else
    package_local_only_absence="passed"
  fi
fi

if [[ -d "${APP_PATH}" ]]; then
  info_plist="${APP_PATH}/Contents/Info.plist"
  if [[ -f "${info_plist}" ]]; then
    bundle_identifier="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "${info_plist}" 2>/dev/null || echo unknown)"
    short_version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "${info_plist}" 2>/dev/null || echo unknown)"
    build_number="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "${info_plist}" 2>/dev/null || echo unknown)"
  fi

  if command -v codesign >/dev/null 2>&1; then
    if codesign --verify --deep --strict "${APP_PATH}" >/dev/null 2>&1; then
      codesign_status="passed"
    else
      codesign_status="failed"
    fi
  fi

  if command -v spctl >/dev/null 2>&1; then
    if spctl --assess --type execute --verbose "${APP_PATH}" >/dev/null 2>&1; then
      spctl_status="passed"
    else
      spctl_status="failed"
    fi
  fi

  if command -v xcrun >/dev/null 2>&1; then
    if xcrun stapler validate "${APP_PATH}" >/dev/null 2>&1; then
      stapling_status="passed"
    else
      stapling_status="failed"
    fi
  fi

  if command -v xattr >/dev/null 2>&1; then
    if xattr -p com.apple.quarantine "${APP_PATH}" >/dev/null 2>&1; then
      quarantine_status="present"
    else
      quarantine_status="absent"
    fi
  fi
fi

mkdir -p "${EVIDENCE_DIR}"
summary_path="${EVIDENCE_DIR}/gatekeeper-qa-summary.json"
cat >"${summary_path}" <<JSON
{
  "schemaVersion": 1,
  "packagePath": "$(json_escape "$(safe_path "${PACKAGE_PATH}")")",
  "appBundlePath": "$(json_escape "$(safe_path "${APP_PATH}")")",
  "packageExists": ${package_exists},
  "packageLocalOnlyFileAbsence": "$(json_escape "${package_local_only_absence}")",
  "codesignStatus": "$(json_escape "${codesign_status}")",
  "spctlStatus": "$(json_escape "${spctl_status}")",
  "staplingStatus": "$(json_escape "${stapling_status}")",
  "quarantineStatus": "$(json_escape "${quarantine_status}")",
  "bundleIdentifier": "$(json_escape "${bundle_identifier}")",
  "version": "$(json_escape "${short_version}")",
  "build": "$(json_escape "${build_number}")",
  "bypassInstructionsPresent": false,
  "quarantineRemoved": false
}
JSON

if grep -Eiq '/Users/|/private/|APPLE_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|xattr -d com\.apple\.quarantine|spctl[[:space:]]+--master-disable|tokenforge-approved-locations\.local\.json|raw[ _-]?sync[ _-]?payload' "${summary_path}"; then
  rm -f "${summary_path}"
  echo "Gatekeeper QA summary contains unsafe content." >&2
  exit 1
fi

echo "TokenForge Gatekeeper QA helper summary"
echo "Package path: $(safe_path "${PACKAGE_PATH}")"
echo "App path: $(safe_path "${APP_PATH}")"
echo "Package local-only file absence: ${package_local_only_absence}"
echo "codesign verification: ${codesign_status}"
echo "spctl assessment: ${spctl_status}"
echo "stapling validation: ${stapling_status}"
echo "quarantine attribute: ${quarantine_status}"
echo "JSON summary: $(safe_path "${summary_path}")"
echo "Gatekeeper protections were not disabled and quarantine was not removed."

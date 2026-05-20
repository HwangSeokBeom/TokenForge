#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
SUMMARY_PATH="${RELEASE_DIR}/credentialed-release-summary.json"
SIGNING_SUMMARY="${RELEASE_DIR}/signing-status-summary.json"
NOTARIZATION_SUMMARY="${RELEASE_DIR}/notarization-status-summary.json"
READINESS_REPORT="${RELEASE_DIR}/release-readiness-report.json"
CREDENTIALED=false

for arg in "$@"; do
  case "${arg}" in
    --credentialed)
      CREDENTIALED=true
      ;;
    --help|-h)
      echo "Usage: scripts/run-credentialed-release.sh [--credentialed]"
      echo "Default mode is a safe dry-run. --credentialed requires Developer ID and Apple notarization credentials."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

status="CRED_RELEASE_DRY_RUN_READY"
dry_run="true"
credentialed_required_for_final_distribution="true"
current_signing_status="unknown"
current_notarization_status="unknown"
stapling_status="notAttempted"
spctl_status="notAttempted"
release_status="unknown"
release_candidate_sha256=""
safe_failure_reason=""
next_required_credentialed_command="scripts/run-credentialed-release.sh --credentialed"

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

json_field() {
  local file="$1"
  local field="$2"
  if [[ -f "${file}" ]]; then
    /usr/bin/python3 -c 'import json,sys; data=json.load(open(sys.argv[1])); print(data.get(sys.argv[2],""))' "${file}" "${field}" 2>/dev/null || true
  fi
}

json_bool_field() {
  local file="$1"
  local field="$2"
  if [[ -f "${file}" ]]; then
    /usr/bin/python3 -c 'import json,sys; data=json.load(open(sys.argv[1])); print("true" if data.get(sys.argv[2]) is True else "false")' "${file}" "${field}" 2>/dev/null || echo "false"
  else
    echo "false"
  fi
}

sha256_for_package() {
  if [[ -f "${PACKAGE_PATH}" ]]; then
    shasum -a 256 "${PACKAGE_PATH}" | awk '{print $1}'
  fi
}

refresh_status_fields() {
  current_signing_status="$(json_field "${SIGNING_SUMMARY}" status)"
  current_notarization_status="$(json_field "${NOTARIZATION_SUMMARY}" notarizationStatus)"
  stapling_status="$(json_field "${NOTARIZATION_SUMMARY}" staplingStatus)"
  spctl_status="$(json_field "${NOTARIZATION_SUMMARY}" spctlStatus)"
  release_status="$(json_field "${READINESS_REPORT}" releaseStatus)"
  release_candidate_sha256="$(sha256_for_package)"
  if [[ -z "${current_signing_status}" ]]; then current_signing_status="unknown"; fi
  if [[ -z "${current_notarization_status}" ]]; then current_notarization_status="unknown"; fi
  if [[ -z "${stapling_status}" ]]; then stapling_status="notAttempted"; fi
  if [[ -z "${spctl_status}" ]]; then spctl_status="notAttempted"; fi
  if [[ -z "${release_status}" ]]; then release_status="unknown"; fi
}

write_summary() {
  mkdir -p "${RELEASE_DIR}"
  refresh_status_fields
  cat >"${SUMMARY_PATH}" <<JSON
{
  "schemaVersion": 1,
  "status": "$(json_escape "${status}")",
  "dryRun": ${dry_run},
  "credentialedRequiredForFinalDistribution": ${credentialed_required_for_final_distribution},
  "currentSigningStatus": "$(json_escape "${current_signing_status}")",
  "currentNotarizationStatus": "$(json_escape "${current_notarization_status}")",
  "staplingStatus": "$(json_escape "${stapling_status}")",
  "spctlStatus": "$(json_escape "${spctl_status}")",
  "releaseStatus": "$(json_escape "${release_status}")",
  "releaseCandidatePath": "$(json_escape "$(safe_path "${PACKAGE_PATH}")")",
  "releaseCandidateSha256": "$(json_escape "${release_candidate_sha256}")",
  "readinessReportPath": "$(json_escape "$(safe_path "${READINESS_REPORT}")")",
  "evidenceBundlePath": "$(json_escape "$(safe_path "${EVIDENCE_DIR}")")",
  "nextRequiredCredentialedCommand": "$(json_escape "${next_required_credentialed_command}")",
  "safeFailureReason": "$(json_escape "${safe_failure_reason}")"
}
JSON
  if grep -Eiq '/Users/|/private/|APPLE_ID|APPLE_TEAM_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|TOKENFORGE_MACOS_SIGN_IDENTITY|BEGIN PRIVATE KEY|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|token[[:space:]]*[:=]|tokenforge-approved-locations\.local\.json|raw[ _-]?sync[ _-]?payload|notarytool submit' "${SUMMARY_PATH}"; then
    rm -f "${SUMMARY_PATH}"
    echo "Credentialed release summary contains unsafe content." >&2
    exit 1
  fi
}

finish() {
  local exit_code="$1"
  write_summary
  PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/generate-release-readiness-report.sh" >/dev/null 2>&1 || true
  PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/collect-release-evidence.sh" >/dev/null 2>&1 || true
  write_summary
  echo "TokenForge credentialed release summary"
  echo "Status: ${status}"
  echo "Dry run: ${dry_run}"
  echo "Credentialed required for final distribution: ${credentialed_required_for_final_distribution}"
  echo "Signing status: ${current_signing_status}"
  echo "Notarization status: ${current_notarization_status}"
  echo "Stapling status: ${stapling_status}"
  echo "spctl status: ${spctl_status}"
  echo "Release status: ${release_status}"
  echo "Release candidate: $(safe_path "${PACKAGE_PATH}")"
  echo "SHA-256: ${release_candidate_sha256:-unknown}"
  echo "Summary: $(safe_path "${SUMMARY_PATH}")"
  echo "Evidence: $(safe_path "${EVIDENCE_DIR}")"
  if [[ -n "${safe_failure_reason}" ]]; then
    echo "Safe failure reason: ${safe_failure_reason}"
  fi
  if [[ "${status}" != "CRED_RELEASE_READY_FOR_DISTRIBUTION" ]]; then
    echo "Next required credentialed command: ${next_required_credentialed_command}"
  fi
  exit "${exit_code}"
}

run_step() {
  local label="$1"
  shift
  echo "==> ${label}"
  if ! "$@"; then
    safe_failure_reason="${label} failed. See normalized summaries under /tmp/tokenforge-release."
    return 1
  fi
}

require_credentials() {
  local missing=()
  [[ -n "${DEVELOPER_ID_APPLICATION:-}" ]] || missing+=("DEVELOPER_ID_APPLICATION")
  [[ -n "${APPLE_ID:-}" ]] || missing+=("APPLE_ID")
  [[ -n "${APPLE_TEAM_ID:-}" ]] || missing+=("APPLE_TEAM_ID")
  [[ -n "${APPLE_APP_SPECIFIC_PASSWORD:-}" ]] || missing+=("APPLE_APP_SPECIFIC_PASSWORD")
  if [[ "${#missing[@]}" -gt 0 ]]; then
    status="CRED_RELEASE_BLOCKED_MISSING_CREDENTIALS"
    safe_failure_reason="Required credential environment variables are missing."
    finish 1
  fi
}

if [[ "${CREDENTIALED}" == "true" ]]; then
  dry_run="false"
  require_credentials
fi

run_step "Validate release metadata" "${SCRIPT_DIR}/validate-release-metadata.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }
run_step "Build macOS smoke" "${SCRIPT_DIR}/build-macos-smoke.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }
run_step "Package macOS smoke" "${SCRIPT_DIR}/package-macos-smoke.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }
run_step "Package release candidate" "${SCRIPT_DIR}/package-macos-release-candidate.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }

if [[ "${CREDENTIALED}" == "true" ]]; then
  if ! run_step "Developer ID sign release candidate" "${SCRIPT_DIR}/sign-macos-release-candidate.sh" --sign; then
    status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"
    finish 1
  fi
  if [[ "$(json_bool_field "${SIGNING_SUMMARY}" signedWithDeveloperId)" != "true" ]]; then
    status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"
    safe_failure_reason="Signing summary did not confirm Developer ID signing."
    finish 1
  fi

  if ! run_step "Submit release candidate for notarization" "${SCRIPT_DIR}/notarize-macos-release-candidate.sh" --submit; then
    notary_error="$(json_field "${NOTARIZATION_SUMMARY}" errorCategory)"
    case "${notary_error}" in
      stapleFailed) status="CRED_RELEASE_BLOCKED_STAPLING_FAILED" ;;
      spctlFailed) status="CRED_RELEASE_BLOCKED_SPCTL_FAILED" ;;
      *) status="CRED_RELEASE_BLOCKED_NOTARIZATION_FAILED" ;;
    esac
    finish 1
  fi

  if [[ "$(json_field "${NOTARIZATION_SUMMARY}" notarizationStatus)" != "NOTARIZATION_SUCCEEDED" ]]; then
    status="CRED_RELEASE_BLOCKED_NOTARIZATION_FAILED"
    safe_failure_reason="Notarization summary did not confirm success."
    finish 1
  fi
  if [[ "$(json_field "${NOTARIZATION_SUMMARY}" staplingStatus)" != "STAPLE_SUCCEEDED" ]]; then
    status="CRED_RELEASE_BLOCKED_STAPLING_FAILED"
    safe_failure_reason="Stapling summary did not confirm success."
    finish 1
  fi
  if [[ "$(json_field "${NOTARIZATION_SUMMARY}" spctlStatus)" != "SPCTL_SUCCEEDED" ]]; then
    status="CRED_RELEASE_BLOCKED_SPCTL_FAILED"
    safe_failure_reason="spctl summary did not confirm success."
    finish 1
  fi
else
  run_step "Signing readiness dry-run" "${SCRIPT_DIR}/sign-macos-release-candidate.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }
  run_step "Notarization readiness dry-run" "${SCRIPT_DIR}/notarize-macos-release-candidate.sh" || { status="CRED_RELEASE_BLOCKED_NOTARIZATION_FAILED"; finish 1; }
fi

write_summary
run_step "Generate readiness report" "${SCRIPT_DIR}/generate-release-readiness-report.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }
run_step "Gatekeeper QA helper" "${SCRIPT_DIR}/gatekeeper-qa-helper.sh" --json || true
run_step "Collect release evidence" "${SCRIPT_DIR}/collect-release-evidence.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }
run_step "Validate QA status" "${SCRIPT_DIR}/validate-gatekeeper-qa-status.sh" || { status="CRED_RELEASE_BLOCKED_SPCTL_FAILED"; finish 1; }
write_summary
if ! run_step "Verify credentialed release gates" "${SCRIPT_DIR}/verify-credentialed-release.sh"; then
  if [[ "${CREDENTIALED}" == "true" ]]; then
    status="CRED_RELEASE_BLOCKED_SPCTL_FAILED"
    finish 1
  fi
fi
run_step "Generate final release record" "${SCRIPT_DIR}/generate-final-release-record.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }
run_step "Refresh readiness report schema v5" "${SCRIPT_DIR}/generate-release-readiness-report.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }
TOKENFORGE_RELEASE_PREP_SUMMARY_ONLY=true run_step "Release prep summary check" "${SCRIPT_DIR}/release-prep-check.sh" || { status="CRED_RELEASE_BLOCKED_SIGNING_FAILED"; finish 1; }

if [[ "${CREDENTIALED}" == "true" ]]; then
  qa_overall="$(/usr/bin/python3 -c 'import json,sys; data=json.load(open(sys.argv[1])); print(data.get("overallStatus","notStarted"))' "${EVIDENCE_DIR}/gatekeeper-qa-status.json" 2>/dev/null || echo "notStarted")"
  if [[ "${qa_overall}" == "passed" ]]; then
    status="CRED_RELEASE_READY_FOR_DISTRIBUTION"
    next_required_credentialed_command=""
  else
    status="CRED_RELEASE_READY_FOR_QA"
    next_required_credentialed_command="run manual QA"
  fi
  credentialed_required_for_final_distribution="false"
else
  status="CRED_RELEASE_DRY_RUN_READY"
fi

write_summary
PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/generate-release-readiness-report.sh" >/dev/null || true
PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/collect-release-evidence.sh" >/dev/null || true
finish 0

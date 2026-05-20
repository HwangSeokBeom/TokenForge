#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

EXPECTED_PRODUCT_NAME="${TOKENFORGE_EXPECTED_PRODUCT_NAME:-TokenForge}"
EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
SIGNING_SUMMARY="${RELEASE_DIR}/signing-status-summary.json"
NOTARIZATION_SUMMARY="${RELEASE_DIR}/notarization-status-summary.json"
CRED_RELEASE_SUMMARY="${RELEASE_DIR}/credentialed-release-summary.json"
VERIFICATION_SUMMARY="${RELEASE_DIR}/credentialed-release-verification.json"
GATEKEEPER_SUMMARY="${EVIDENCE_DIR}/gatekeeper-qa-summary.json"
QA_STATUS="${EVIDENCE_DIR}/gatekeeper-qa-status.json"
EVIDENCE_JSON="${EVIDENCE_DIR}/release-evidence.json"
EVIDENCE_MD="${EVIDENCE_DIR}/release-evidence.md"
HANDOFF_MD="${EVIDENCE_DIR}/final-release-handoff.md"
FINAL_RECORD_MD="${EVIDENCE_DIR}/final-release-record.md"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
CHECKSUMS="${EVIDENCE_DIR}/checksums.txt"

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

json_nested_field() {
  local file="$1"
  local first="$2"
  local second="$3"
  if [[ -f "${file}" ]]; then
    /usr/bin/python3 -c 'import json,sys; data=json.load(open(sys.argv[1])); value=data.get(sys.argv[2],{}); print(value.get(sys.argv[3],"") if isinstance(value,dict) else "")' "${file}" "${first}" "${second}" 2>/dev/null || true
  fi
}

reject_unsafe_file() {
  local file="$1"
  if [[ -f "${file}" ]] && grep -Eiq '/Users/|/private/|BEGIN PRIVATE KEY|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|token[[:space:]]*[:=]|Bearer[[:space:]]+[A-Za-z0-9._-]{20,}|tokenforge-approved-locations\.local\.json|tokenforge-sync-retry-queue\.local\.json|tokenforge-sync-tombstones\.local\.json|tokenforge-sync-conflicts\.local\.json|tokenforge-sync-conflict-audit\.local\.json|tokenforge-sync-local-state\.local\.json|raw[ _-]?sync[ _-]?payload|stack trace|Exception:|notarytool submit|prompt:|response:' "${file}"; then
    echo "Evidence file contains unsafe content: $(basename "${file}")" >&2
    rm -f "${file}"
    exit 1
  fi
}

mkdir -p "${EVIDENCE_DIR}"

PACKAGE_SHA256=""
PACKAGE_EXISTS=false
if [[ -f "${PACKAGE_PATH}" ]]; then
  PACKAGE_EXISTS=true
  PACKAGE_SHA256="$(shasum -a 256 "${PACKAGE_PATH}" | awk '{print $1}')"
  printf '%s  %s\n' "${PACKAGE_SHA256}" "$(safe_path "${PACKAGE_PATH}")" >"${CHECKSUMS}"
else
  : >"${CHECKSUMS}"
fi

PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${SCRIPT_DIR}/generate-release-readiness-report.sh" >/dev/null

PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${SCRIPT_DIR}/gatekeeper-qa-helper.sh" --json >/dev/null || true

for source in "${READINESS_REPORT}" "${SIGNING_SUMMARY}" "${NOTARIZATION_SUMMARY}" "${CRED_RELEASE_SUMMARY}" "${VERIFICATION_SUMMARY}" "${GATEKEEPER_SUMMARY}" "${QA_STATUS}" "${FINAL_RECORD_MD}" "${FINAL_RECORD_JSON}"; do
  if [[ -f "${source}" ]]; then
    destination="${EVIDENCE_DIR}/$(basename "${source}")"
    if [[ "${source}" != "${destination}" ]]; then
      cp "${source}" "${destination}"
    fi
  fi
done

signing_status="$(json_field "${SIGNING_SUMMARY}" status)"
signing_identity_type="$(json_field "${SIGNING_SUMMARY}" signingIdentityType)"
hardened_runtime="$(json_field "${SIGNING_SUMMARY}" hardenedRuntime)"
notarization_status="$(json_field "${NOTARIZATION_SUMMARY}" notarizationStatus)"
stapling_status="$(json_field "${NOTARIZATION_SUMMARY}" staplingStatus)"
spctl_status="$(json_field "${NOTARIZATION_SUMMARY}" spctlStatus)"
release_status="$(json_field "${READINESS_REPORT}" releaseStatus)"
package_scan="$(json_field "${READINESS_REPORT}" packageScanResult)"
privacy_scan="$(json_field "${READINESS_REPORT}" privacyScanResult)"
gatekeeper_qa_status="$(json_nested_field "${READINESS_REPORT}" qaStatusSummary overallStatus)"
credentialed_status="$(json_field "${CRED_RELEASE_SUMMARY}" status)"
verification_status="$(json_field "${VERIFICATION_SUMMARY}" status)"

next_action="blocked with reason"
known_limitations="See readiness report v4."
if [[ "${release_status}" == "readyForDistribution" ]]; then
  next_action="ready for distribution"
elif [[ "${release_status}" == "pendingQa" ]]; then
  next_action="run manual QA"
elif [[ "${release_status}" == "dryRunReady" || "${notarization_status}" == "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING" || "${signing_status}" == "SIGNING_READY_BUT_IDENTITY_MISSING" ]]; then
  next_action="run credentialed release"
fi

cat >"${EVIDENCE_JSON}" <<JSON
{
  "schemaVersion": 2,
  "schemaName": "tokenforge.releaseEvidence.v2",
  "appName": "$(json_escape "${EXPECTED_PRODUCT_NAME}")",
  "version": "$(json_escape "${EXPECTED_VERSION}")",
  "build": "$(json_escape "${EXPECTED_BUILD_NUMBER}")",
  "releaseCandidatePath": "$(json_escape "$(safe_path "${PACKAGE_PATH}")")",
  "releaseCandidateExists": ${PACKAGE_EXISTS},
  "releaseCandidateSha256": "$(json_escape "${PACKAGE_SHA256}")",
  "readinessReportPath": "$(json_escape "$(safe_path "${READINESS_REPORT}")")",
  "signingSummaryPath": "$(json_escape "$(safe_path "${SIGNING_SUMMARY}")")",
  "notarizationSummaryPath": "$(json_escape "$(safe_path "${NOTARIZATION_SUMMARY}")")",
  "credentialedReleaseSummaryPath": "$(json_escape "$(safe_path "${CRED_RELEASE_SUMMARY}")")",
  "credentialedReleaseVerificationPath": "$(json_escape "$(safe_path "${VERIFICATION_SUMMARY}")")",
  "finalReleaseHandoffPath": "$(json_escape "$(safe_path "${HANDOFF_MD}")")",
  "finalReleaseRecordPath": "$(json_escape "$(safe_path "${FINAL_RECORD_MD}")")",
  "signingStatus": "$(json_escape "${signing_status:-unknown}")",
  "signingIdentityType": "$(json_escape "${signing_identity_type:-unknown}")",
  "hardenedRuntime": "$(json_escape "${hardened_runtime:-unknown}")",
  "notarizationStatus": "$(json_escape "${notarization_status:-NOTARIZATION_NOT_ATTEMPTED}")",
  "staplingStatus": "$(json_escape "${stapling_status:-notAttempted}")",
  "spctlStatus": "$(json_escape "${spctl_status:-notAttempted}")",
  "releaseStatus": "$(json_escape "${release_status:-unknown}")",
  "credentialedReleaseStatus": "$(json_escape "${credentialed_status:-notRun}")",
  "credentialedReleaseVerificationStatus": "$(json_escape "${verification_status:-notRun}")",
  "privacyScanResult": "$(json_escape "${privacy_scan:-unknown}")",
  "packageScanResult": "$(json_escape "${package_scan:-unknown}")",
  "gatekeeperQaStatus": "$(json_escape "${gatekeeper_qa_status:-notStarted}")",
  "gatekeeperQaStatusPath": "$(json_escape "$(safe_path "${QA_STATUS}")")",
  "nextAction": "$(json_escape "${next_action}")",
  "localOnlyEvidenceBoundary": "Evidence is stored outside the package under /tmp/tokenforge-release/evidence."
}
JSON

cat >"${EVIDENCE_MD}" <<MD
# TokenForge Release Evidence

- App: ${EXPECTED_PRODUCT_NAME}
- Version: ${EXPECTED_VERSION}
- Build: ${EXPECTED_BUILD_NUMBER}
- Release candidate: $(safe_path "${PACKAGE_PATH}")
- SHA-256: ${PACKAGE_SHA256:-not available}
- Readiness report: $(safe_path "${READINESS_REPORT}")
- Signing status: ${signing_status:-unknown}
- Signing identity type: ${signing_identity_type:-unknown}
- Hardened runtime: ${hardened_runtime:-unknown}
- Notarization status: ${notarization_status:-NOTARIZATION_NOT_ATTEMPTED}
- Stapling status: ${stapling_status:-notAttempted}
- spctl status: ${spctl_status:-notAttempted}
- Privacy scan: ${privacy_scan:-unknown}
- Package scan: ${package_scan:-unknown}
- Gatekeeper QA status: ${gatekeeper_qa_status:-notStarted}
- Next action: ${next_action}

## Evidence Boundary

Evidence is local-only under $(safe_path "${EVIDENCE_DIR}") and is not included in the app package. It contains summaries and checksums only, not private local data, approved-location paths, raw logs, or credential material.
MD

cat >"${HANDOFF_MD}" <<MD
# TokenForge Final Release Handoff

- App version/build: ${EXPECTED_VERSION} (${EXPECTED_BUILD_NUMBER})
- Release candidate path: $(safe_path "${PACKAGE_PATH}")
- SHA-256: ${PACKAGE_SHA256:-not available}
- Signing status: ${signing_status:-unknown}
- Notarization status: ${notarization_status:-NOTARIZATION_NOT_ATTEMPTED}
- Stapling status: ${stapling_status:-notAttempted}
- spctl status: ${spctl_status:-notAttempted}
- Privacy scan status: ${privacy_scan:-unknown}
- Package scan status: ${package_scan:-unknown}
- QA status: ${gatekeeper_qa_status:-notStarted}
- Release status: ${release_status:-unknown}
- Known limitations: ${known_limitations}
- Exact next action: ${next_action}

This handoff contains normalized release summaries only. It omits private local data, credential material, diagnostic dumps, repository metadata, and approved-location details.
MD

for file in "${READINESS_REPORT}" "${SIGNING_SUMMARY}" "${NOTARIZATION_SUMMARY}" "${CRED_RELEASE_SUMMARY}" "${VERIFICATION_SUMMARY}" "${GATEKEEPER_SUMMARY}" "${QA_STATUS}" "${EVIDENCE_JSON}" "${EVIDENCE_MD}" "${HANDOFF_MD}" "${FINAL_RECORD_MD}" "${FINAL_RECORD_JSON}" "${CHECKSUMS}" "${EVIDENCE_DIR}/$(basename "${READINESS_REPORT}")" "${EVIDENCE_DIR}/$(basename "${SIGNING_SUMMARY}")" "${EVIDENCE_DIR}/$(basename "${NOTARIZATION_SUMMARY}")" "${EVIDENCE_DIR}/$(basename "${CRED_RELEASE_SUMMARY}")" "${EVIDENCE_DIR}/$(basename "${VERIFICATION_SUMMARY}")"; do
  reject_unsafe_file "${file}"
done

echo "TokenForge release evidence bundle"
echo "Evidence path: $(safe_path "${EVIDENCE_DIR}")"
echo "Evidence JSON: $(safe_path "${EVIDENCE_JSON}")"
echo "Evidence markdown: $(safe_path "${EVIDENCE_MD}")"
echo "Final release handoff: $(safe_path "${HANDOFF_MD}")"
echo "Checksums: $(safe_path "${CHECKSUMS}")"
echo "Release candidate SHA-256: ${PACKAGE_SHA256:-not available}"

#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
SUMMARY_PATH="${RELEASE_DIR}/release-machine-finalization-summary.json"
CREDENTIALED=false

for arg in "$@"; do
  case "${arg}" in
    --credentialed)
      CREDENTIALED=true
      ;;
    --help|-h)
      echo "Usage: scripts/run-release-machine-finalization.sh [--credentialed]"
      echo "Default mode writes a safe checklist summary. --credentialed runs the final release-machine sequence."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

mkdir -p "${RELEASE_DIR}" "${EVIDENCE_DIR}"

run_step() {
  local label="$1"
  shift
  echo "==> ${label}"
  "$@"
}

missing_credentials=()
if [[ -z "${DEVELOPER_ID_APPLICATION:-}" ]]; then missing_credentials+=("DEVELOPER_ID_APPLICATION"); fi
if [[ -z "${APPLE_ID:-}" ]]; then missing_credentials+=("APPLE_ID"); fi
if [[ -z "${APPLE_TEAM_ID:-}" ]]; then missing_credentials+=("APPLE_TEAM_ID"); fi
if [[ -z "${APPLE_APP_SPECIFIC_PASSWORD:-}" ]]; then missing_credentials+=("APPLE_APP_SPECIFIC_PASSWORD"); fi

if [[ "${CREDENTIALED}" == "true" && "${#missing_credentials[@]}" -gt 0 ]]; then
  TOKENFORGE_RELEASE_MACHINE_CRED_ATTEMPTED=true \
  TOKENFORGE_RELEASE_MACHINE_BLOCKED_REASON="Missing required credential environment variables: ${missing_credentials[*]}" \
  PACKAGE_PATH="${PACKAGE_PATH}" \
  /usr/bin/python3 "${SCRIPT_DIR}/../scripts/phase31_release_machine_summary.py" "${SUMMARY_PATH}" "${RELEASE_DIR}" "${EVIDENCE_DIR}" "${PACKAGE_PATH}"
  exit 1
fi

if [[ "${CREDENTIALED}" == "true" ]]; then
  if ! run_step "Release prep check before credentialed finalization" "${SCRIPT_DIR}/release-prep-check.sh"; then
    TOKENFORGE_RELEASE_MACHINE_CRED_ATTEMPTED=true TOKENFORGE_RELEASE_MACHINE_BLOCKED_REASON="Release prep check failed before credentialed finalization." PACKAGE_PATH="${PACKAGE_PATH}" /usr/bin/python3 "${SCRIPT_DIR}/../scripts/phase31_release_machine_summary.py" "${SUMMARY_PATH}" "${RELEASE_DIR}" "${EVIDENCE_DIR}" "${PACKAGE_PATH}"
    exit 1
  fi
  if ! run_step "Credentialed release" "${SCRIPT_DIR}/run-credentialed-release.sh" --credentialed; then
    TOKENFORGE_RELEASE_MACHINE_CRED_ATTEMPTED=true TOKENFORGE_RELEASE_MACHINE_BLOCKED_REASON="Credentialed release execution failed." PACKAGE_PATH="${PACKAGE_PATH}" /usr/bin/python3 "${SCRIPT_DIR}/../scripts/phase31_release_machine_summary.py" "${SUMMARY_PATH}" "${RELEASE_DIR}" "${EVIDENCE_DIR}" "${PACKAGE_PATH}"
    exit 1
  fi
  run_step "Verify credentialed release" "${SCRIPT_DIR}/verify-credentialed-release.sh" || true
  run_step "Gatekeeper QA helper" "${SCRIPT_DIR}/gatekeeper-qa-helper.sh" || true
  run_step "Gatekeeper QA summary" "${SCRIPT_DIR}/update-gatekeeper-qa-status.sh" summary || true
  run_step "Generate final release record" "${SCRIPT_DIR}/generate-final-release-record.sh" || true
  run_step "Generate release operator handoff" "${SCRIPT_DIR}/generate-release-operator-handoff.sh" || true
  run_step "Final privacy regression audit" "${SCRIPT_DIR}/final-privacy-regression-audit.sh" || true
  run_step "Release prep check after credentialed finalization" "${SCRIPT_DIR}/release-prep-check.sh" || true
else
  PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/generate-release-readiness-report.sh" >/dev/null || true
  PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/verify-credentialed-release.sh" >/dev/null || true
  PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/final-privacy-regression-audit.sh" >/dev/null || true
  PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/validate-release-freeze-manifest.sh" >/dev/null || true
fi

TOKENFORGE_RELEASE_MACHINE_CRED_ATTEMPTED="${CREDENTIALED}" \
PACKAGE_PATH="${PACKAGE_PATH}" \
/usr/bin/python3 "${SCRIPT_DIR}/../scripts/phase31_release_machine_summary.py" "${SUMMARY_PATH}" "${RELEASE_DIR}" "${EVIDENCE_DIR}" "${PACKAGE_PATH}" || {
  if [[ "${CREDENTIALED}" == "true" ]]; then
    exit 1
  fi
  exit 0
}

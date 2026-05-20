#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
FINALIZATION_SUMMARY="${RELEASE_DIR}/release-machine-finalization-summary.json"
VERIFICATION_SUMMARY="${RELEASE_DIR}/credentialed-release-verification.json"
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
FINAL_PRIVACY_AUDIT="${EVIDENCE_DIR}/final-privacy-regression-audit.json"
EVIDENCE_LOCK="${EVIDENCE_DIR}/evidence-lock-summary.json"
FREEZE_VALIDATION="${EVIDENCE_DIR}/release-freeze-validation.json"
FINAL_DISTRIBUTION="${RELEASE_DIR}/final-distribution-readiness.json"
CREATE=false
PUSH=false
YES=false
SKIP_PREP_CHECK="${TOKENFORGE_RELEASE_TAG_SKIP_PREP_CHECK:-false}"

for arg in "$@"; do
  case "${arg}" in
    --create)
      CREATE=true
      ;;
    --push)
      PUSH=true
      ;;
    --yes)
      YES=true
      ;;
    --help|-h)
      echo "Usage: scripts/prepare-release-tag.sh [--create --yes] [--push --yes]"
      echo "Dry-run prints the suggested release tag and gate state. It never creates or pushes a tag."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

if [[ "${CREATE}" == "true" && "${YES}" != "true" ]]; then
  echo "--create requires --yes." >&2
  exit 1
fi

if [[ "${PUSH}" == "true" && "${YES}" != "true" ]]; then
  echo "--push requires --yes." >&2
  exit 1
fi

read_metadata() {
  if [[ -f "${READINESS_REPORT}" ]]; then
    /usr/bin/python3 - "${READINESS_REPORT}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" <<'PY'
import json
import sys

path, fallback_version, fallback_build = sys.argv[1:]
try:
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
except Exception:
    data = {}
print(data.get("version") or data.get("appVersion") or fallback_version)
print(data.get("build") or data.get("appBuild") or fallback_build)
PY
  else
    printf '%s\n%s\n' "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}"
  fi
}

metadata="$(read_metadata)"
VERSION="$(printf '%s\n' "${metadata}" | sed -n '1p')"
BUILD="$(printf '%s\n' "${metadata}" | sed -n '2p')"
if [[ -z "${VERSION}" ]]; then VERSION="${EXPECTED_VERSION}"; fi
if [[ -z "${BUILD}" ]]; then BUILD="${EXPECTED_BUILD_NUMBER}"; fi
TAG="v${VERSION}-build${BUILD}"

if [[ ! "${VERSION}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ || ! "${BUILD}" =~ ^[0-9]+$ ]]; then
  echo "Release metadata did not provide a safe version/build." >&2
  exit 1
fi

cd "${REPO_ROOT}"

dirty_status="$(git status --short)"
if [[ -n "${dirty_status}" ]]; then
  echo "Working tree status: dirty"
  echo "Dirty working tree warning: review local changes before creating or pushing the release tag."
else
  echo "Working tree status: clean"
fi

if [[ "${SKIP_PREP_CHECK}" != "true" && "${CREATE}${PUSH}" != "falsefalse" ]]; then
  "${SCRIPT_DIR}/release-prep-check.sh" --summary-only >/dev/null
  echo "Release prep check: passed"
elif [[ "${SKIP_PREP_CHECK}" == "true" ]]; then
  echo "Release prep check: skipped for release-prep integration"
fi

PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${VERSION}-build${BUILD}.zip}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${SCRIPT_DIR}/verify-final-distribution-readiness.sh" >/dev/null || true

gate_json="$(
  /usr/bin/python3 - "${READINESS_REPORT}" "${FINALIZATION_SUMMARY}" "${VERIFICATION_SUMMARY}" "${QA_STATUS}" "${FINAL_PRIVACY_AUDIT}" "${EVIDENCE_LOCK}" "${FREEZE_VALIDATION}" "${FINAL_DISTRIBUTION}" <<'PY'
import json
import os
import sys

readiness_path, finalization_path, verification_path, qa_path, audit_path, lock_path, freeze_path, final_distribution_path = sys.argv[1:]

def load(path):
    if not os.path.exists(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data if isinstance(data, dict) else {}
    except Exception:
        return {}

readiness = load(readiness_path)
finalization = load(finalization_path)
verification = load(verification_path)
qa = load(qa_path)
audit = load(audit_path)
lock = load(lock_path)
freeze = load(freeze_path)
final_distribution = load(final_distribution_path)
qa_status = qa.get("qaOverallStatus", qa.get("overallStatus", readiness.get("qaStatusSummary", {}).get("overallStatus", "missing")))
reasons = []
checks = {
    "finalDistributionStatus": final_distribution.get("status", "missing"),
    "releaseMachineFinalization": finalization.get("status", "missing"),
    "readinessReleaseStatus": readiness.get("releaseStatus", "missing"),
    "verificationStatus": verification.get("status", "missing"),
    "qaOverallStatus": qa_status,
    "finalPrivacyAuditStatus": audit.get("status", "missing"),
    "evidenceLockStatus": lock.get("status", "missing"),
    "freezeValidationStatus": freeze.get("status", "missing"),
    "tagAllowed": final_distribution.get("tagAllowed", False),
}
expected = {
    "finalDistributionStatus": "FINAL_DISTRIBUTION_READY",
    "releaseMachineFinalization": "RELEASE_MACHINE_READY_FOR_TAG",
    "readinessReleaseStatus": "readyForDistribution",
    "verificationStatus": "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION",
    "qaOverallStatus": "passed",
    "finalPrivacyAuditStatus": "FINAL_PRIVACY_AUDIT_PASSED",
    "evidenceLockStatus": "EVIDENCE_LOCK_READY",
    "freezeValidationStatus": "FREEZE_MANIFEST_VALID_FINAL_MATCH",
}
for key, value in expected.items():
    if checks.get(key) != value:
        reasons.append(f"{key} is {checks.get(key)}")
if checks.get("tagAllowed") is not True:
    reasons.append("final distribution verifier did not allow tag creation")
sha = final_distribution.get("releaseCandidateSha256") or readiness.get("releaseCandidateSha256", "")
print(json.dumps({"allowed": not reasons, "checks": checks, "blockedReasons": reasons, "releaseCandidateSha256": sha}, sort_keys=True))
PY
)"

tag_allowed="$(printf '%s' "${gate_json}" | /usr/bin/python3 -c 'import json,sys; print("true" if json.load(sys.stdin)["allowed"] else "false")')"
gate_summary="$(printf '%s' "${gate_json}" | /usr/bin/python3 -c 'import json,sys; data=json.load(sys.stdin); print("; ".join(data["blockedReasons"]))')"
final_sha="$(printf '%s' "${gate_json}" | /usr/bin/python3 -c 'import json,sys; print(json.load(sys.stdin).get("releaseCandidateSha256",""))')"
evidence_lock_status="$(printf '%s' "${gate_json}" | /usr/bin/python3 -c 'import json,sys; print(json.load(sys.stdin)["checks"].get("evidenceLockStatus","missing"))')"
MESSAGE="$(printf 'TokenForge macOS %s build %s\n\nMVP release\nSHA-256: %s\nEvidence lock status: %s' "${VERSION}" "${BUILD}" "${final_sha:-not available}" "${evidence_lock_status}")"

echo "Suggested tag: ${TAG}"
echo "Tag message: ${MESSAGE}"
echo "Create tag command: git tag -a ${TAG} -m \"${MESSAGE}\""
echo "Push tag command: git push origin ${TAG}"
echo "Tag currently allowed: ${tag_allowed}"
if [[ -n "${gate_summary}" ]]; then
  echo "Tag blocked reasons: ${gate_summary}"
fi

if [[ "${CREATE}" != "true" && "${PUSH}" != "true" ]]; then
  echo "Result: dry-run only; no tag created or pushed"
  exit 0
fi

if [[ "${tag_allowed}" != "true" ]]; then
  echo "TAG_BLOCKED_RELEASE_NOT_READY" >&2
  exit 1
fi

tag_exists=false
if git rev-parse -q --verify "refs/tags/${TAG}" >/dev/null; then
  tag_exists=true
fi

if [[ "${CREATE}" == "true" ]]; then
  if [[ "${tag_exists}" == "true" ]]; then
    echo "Suggested tag already exists locally: ${TAG}" >&2
    exit 1
  fi
  git tag -a "${TAG}" -m "${MESSAGE}"
  tag_exists=true
  echo "Created tag: ${TAG}"
fi

if [[ "${PUSH}" == "true" ]]; then
  if [[ "${tag_exists}" != "true" ]]; then
    echo "Local tag does not exist: ${TAG}" >&2
    exit 1
  fi
  git push origin "${TAG}"
  echo "Pushed tag: ${TAG}"
elif [[ "${CREATE}" == "true" ]]; then
  echo "Result: tag created locally; not pushed"
fi

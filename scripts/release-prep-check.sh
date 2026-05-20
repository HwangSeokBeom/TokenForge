#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${REPO_ROOT}/UnityClient"

UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity}"
EDITMODE_RESULTS="${EDITMODE_RESULTS:-/tmp/tokenforge-editmode-results.xml}"
PLAYMODE_RESULTS="${PLAYMODE_RESULTS:-/tmp/tokenforge-playmode-results.xml}"
EDITMODE_LOG="${EDITMODE_LOG:-/tmp/tokenforge-unity-editmode.log}"
PLAYMODE_LOG="${PLAYMODE_LOG:-/tmp/tokenforge-unity-playmode.log}"
CONTRACT_BUNDLE_OUTPUT="${CONTRACT_BUNDLE_OUTPUT:-/tmp/unity-safe-sync-contract-v1.bundle.json}"
CONTRACT_EXPORT_LOG="${CONTRACT_EXPORT_LOG:-/tmp/tokenforge-contract-export.log}"
SUMMARY_ONLY="${TOKENFORGE_RELEASE_PREP_SUMMARY_ONLY:-false}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"

validate_phase29_summaries() {
  /usr/bin/python3 - "${RELEASE_DIR}" "${EVIDENCE_DIR}" <<'PY'
import json
import os
import sys

release_dir, evidence_dir = sys.argv[1:]

def load(name):
    path = os.path.join(release_dir, name)
    if not os.path.exists(path):
        return {}
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    return data if isinstance(data, dict) else {}

readiness = load("release-readiness-report.json")
signing = load("signing-status-summary.json")
notary = load("notarization-status-summary.json")
verification = load("credentialed-release-verification.json")
record_path = os.path.join(evidence_dir, "final-release-record.json")
record = {}
if os.path.exists(record_path):
    with open(record_path, "r", encoding="utf-8") as handle:
        record = json.load(handle)

if readiness.get("schemaVersion") != 5:
    raise SystemExit("Readiness report schemaVersion must be 5.")

release_status = readiness.get("releaseStatus")
signed = signing.get("signedWithDeveloperId") is True and signing.get("signingIdentityType") == "developerId"
hardened = signing.get("hardenedRuntime") == "enabled"
notarized = notary.get("notarizationStatus") == "NOTARIZATION_SUCCEEDED"
stapled = notary.get("staplingStatus") == "STAPLE_SUCCEEDED"
spctl = notary.get("spctlStatus") == "SPCTL_SUCCEEDED"
privacy = readiness.get("packageScanResult") == "passed" and readiness.get("privacyScanResult") == "passed"
qa = readiness.get("qaStatusSummary", {}).get("overallStatus") == "passed"
audit = readiness.get("finalPrivacyAuditStatus", "")
lock = readiness.get("evidenceLockSummary", {}).get("status")

if release_status == "readyForDistribution" and not all([signed, hardened, notarized, stapled, spctl, privacy, qa, lock == "EVIDENCE_LOCK_READY"]):
    raise SystemExit("readyForDistribution was reported without all required release gates.")
if signing.get("signingIdentityType") == "adHoc" and release_status == "readyForDistribution":
    raise SystemExit("Ad-hoc dry-run release must not report readyForDistribution.")
if verification and verification.get("status") == "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION" and release_status != "readyForDistribution":
    raise SystemExit("Credentialed verification and readiness report disagree.")
if record and record.get("distributionReadiness") == "ready for distribution" and release_status != "readyForDistribution":
    raise SystemExit("Final release record claims distribution readiness without readiness report support.")

print("Phase 29 summary validation: passed")
PY
}

validate_phase31_summaries() {
  /usr/bin/python3 - "${RELEASE_DIR}" "${EVIDENCE_DIR}" <<'PY'
import json
import os
import subprocess
import sys

release_dir, evidence_dir = sys.argv[1:]

def load(path):
    if not os.path.exists(path):
        return {}
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    return data if isinstance(data, dict) else {}

readiness = load(os.path.join(release_dir, "release-readiness-report.json"))
freeze = load(os.path.join(evidence_dir, "release-freeze-validation.json"))
finalization = load(os.path.join(release_dir, "release-machine-finalization-summary.json"))
lock = load(os.path.join(evidence_dir, "evidence-lock-summary.json"))
audit = load(os.path.join(evidence_dir, "final-privacy-regression-audit.json"))
verification = load(os.path.join(release_dir, "credentialed-release-verification.json"))

if readiness.get("schemaVersion") != 5:
    raise SystemExit("Phase 31 requires readiness report schemaVersion 5.")
if readiness.get("releaseStatus") == "readyForDistribution" and readiness.get("evidenceLockSummary", {}).get("status") != "EVIDENCE_LOCK_READY":
    raise SystemExit("readyForDistribution requires evidence lock ready.")
if freeze and str(freeze.get("status", "")).startswith("FREEZE_MANIFEST_BLOCKED"):
    raise SystemExit("Release freeze manifest validation is blocked.")
if finalization and finalization.get("status") == "RELEASE_MACHINE_READY_FOR_TAG":
    required = [
        readiness.get("releaseStatus") in {"readyForDistribution", "evidencePending"},
        verification.get("status") == "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION",
        audit.get("status") == "FINAL_PRIVACY_AUDIT_PASSED",
    ]
    if not all(required):
        raise SystemExit("Release-machine ready-for-tag summary is inconsistent with release gates.")
if lock and lock.get("status") == "EVIDENCE_LOCK_READY" and readiness.get("releaseStatus") not in {"readyForDistribution", "evidencePending"}:
    raise SystemExit("Evidence lock ready is inconsistent with readiness report.")

print("Phase 31 summary validation: passed")
PY
}

validate_phase32_summaries() {
  /usr/bin/python3 - "${RELEASE_DIR}" "${EVIDENCE_DIR}" <<'PY'
import json
import os
import re
import subprocess
import sys

release_dir, evidence_dir = sys.argv[1:]
unsafe = re.compile(
    r"/Users/|/private/|~/|BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"tokenforge-approved-locations\.local\.json|tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|raw server|prompt:|response:|Traceback \(most recent call last\)|stack trace|"
    r"xattr -d com\.apple\.quarantine|spctl\s+--master-disable",
    re.I,
)

def load(path):
    if not os.path.exists(path):
        raise SystemExit(f"Missing Phase 32 output: {path}")
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    return data if isinstance(data, dict) else {}

final_distribution = load(os.path.join(release_dir, "final-distribution-readiness.json"))
draft = load(os.path.join(evidence_dir, "github-release-draft.json"))
bundle = load(os.path.join(evidence_dir, "release-bundle-index.json"))
dashboard = load(os.path.join(release_dir, "release-status-dashboard.json"))
readiness = load(os.path.join(release_dir, "release-readiness-report.json"))
lock = load(os.path.join(evidence_dir, "evidence-lock-summary.json"))

if final_distribution.get("status") == "FINAL_DISTRIBUTION_READY":
    if readiness.get("releaseStatus") != "readyForDistribution":
        raise SystemExit("Final distribution ready without readiness readyForDistribution.")
    if lock.get("status") != "EVIDENCE_LOCK_READY":
        raise SystemExit("Final distribution ready without evidence lock ready.")
else:
    if final_distribution.get("tagAllowed") is True:
        raise SystemExit("Tag was allowed without final distribution readiness.")
    if readiness.get("releaseStatus") == "readyForDistribution":
        raise SystemExit("Readiness report claims readyForDistribution while final distribution is not ready.")

if draft.get("finalDistributionStatus") != final_distribution.get("status"):
    raise SystemExit("GitHub Release draft and final distribution summary disagree.")
if draft.get("finalDistributionStatus") != "FINAL_DISTRIBUTION_READY" and draft.get("installationNotesIncludedForPublicDistribution") is True:
    raise SystemExit("GitHub Release draft overclaims public install readiness.")
if draft.get("notarizationStatus") != "NOTARIZATION_SUCCEEDED" and "notarization, stapling, and spctl assessment are complete" in open(os.path.join(evidence_dir, "github-release-draft.md"), encoding="utf-8").read():
    raise SystemExit("GitHub Release draft overclaims notarization.")
if bundle.get("distributionReady") != (final_distribution.get("status") == "FINAL_DISTRIBUTION_READY"):
    raise SystemExit("Release bundle index distributionReady is inconsistent.")
if dashboard.get("finalDistributionStatus") != final_distribution.get("status"):
    raise SystemExit("Status dashboard and final distribution summary disagree.")

for path in [
    os.path.join(release_dir, "final-distribution-readiness.json"),
    os.path.join(evidence_dir, "github-release-draft.md"),
    os.path.join(evidence_dir, "github-release-draft.json"),
    os.path.join(evidence_dir, "release-bundle-index.md"),
    os.path.join(evidence_dir, "release-bundle-index.json"),
    os.path.join(release_dir, "release-status-dashboard.json"),
]:
    with open(path, "r", encoding="utf-8") as handle:
        if unsafe.search(handle.read()):
            raise SystemExit(f"Phase 32 output contains unsafe content: {os.path.basename(path)}")

print("Phase 32 summary validation: passed")
PY
}

validate_phase33_summaries() {
  /usr/bin/python3 - "${RELEASE_DIR}" "${EVIDENCE_DIR}" "${REPO_ROOT}" <<'PY'
import json
import os
import re
import sys

release_dir, evidence_dir, repo_root = sys.argv[1:]
unsafe = re.compile(
    r"/Users/|/private/|~/|BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"tokenforge-approved-locations\.local\.json|tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|raw server|prompt:|response:|Traceback \(most recent call last\)|stack trace|"
    r"xattr -d com\.apple\.quarantine|spctl\s+--master-disable",
    re.I,
)

def load(path):
    if not os.path.exists(path):
        raise SystemExit(f"Missing Phase 33 output: {path}")
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    return data if isinstance(data, dict) else {}

final_distribution = load(os.path.join(release_dir, "final-distribution-readiness.json"))
assets = load(os.path.join(release_dir, "github-release-assets-validation.json"))
plan = load(os.path.join(evidence_dir, "github-release-publication-plan.json"))
publication = load(os.path.join(release_dir, "github-release-publication-summary.json"))
audit = load(os.path.join(evidence_dir, "post-release-audit.json"))
dashboard = load(os.path.join(release_dir, "release-status-dashboard.json"))
lock = load(os.path.join(evidence_dir, "evidence-lock-summary.json"))

final_ready = final_distribution.get("status") == "FINAL_DISTRIBUTION_READY"
if assets.get("status") == "GITHUB_RELEASE_ASSETS_READY" and not final_ready:
    raise SystemExit("GitHub assets became ready without final distribution readiness.")
if assets.get("status") == "GITHUB_RELEASE_ASSETS_BLOCKED":
    raise SystemExit("GitHub asset validation is blocked.")
if plan.get("status") == "GITHUB_RELEASE_PUBLICATION_PLAN_READY" and assets.get("status") != "GITHUB_RELEASE_ASSETS_READY":
    raise SystemExit("GitHub publication plan became ready without validated assets.")
if publication.get("status") in {"GITHUB_RELEASE_PUBLISHED", "GITHUB_RELEASE_ALREADY_EXISTS"} and not final_ready:
    raise SystemExit("GitHub publication summary claims publication without final distribution readiness.")
if publication.get("status") in {"GITHUB_RELEASE_PUBLISHED", "GITHUB_RELEASE_ALREADY_EXISTS"} and audit.get("tagExistsLocally") is not True:
    raise SystemExit("GitHub publication summary claims publication without a local guarded tag.")
if publication.get("status") == "GITHUB_RELEASE_DRY_RUN_READY" and not final_ready:
    raise SystemExit("GitHub publish dry-run became ready without final distribution readiness.")
if publication.get("status") == "GITHUB_RELEASE_DRY_RUN_READY" and assets.get("status") != "GITHUB_RELEASE_ASSETS_READY":
    raise SystemExit("GitHub publish dry-run became ready without asset validation.")
if final_ready and lock.get("status") != "EVIDENCE_LOCK_READY":
    raise SystemExit("Final distribution ready without evidence lock ready.")
if not final_ready and audit.get("published") is True:
    raise SystemExit("Post-release audit claims publication without final distribution readiness.")
if audit.get("published") is False and audit.get("githubReleaseUrl"):
    raise SystemExit("Post-release audit includes a release URL without publication.")
if dashboard.get("schemaName") != "tokenforge.releaseStatusDashboard.v2":
    raise SystemExit("Release status dashboard is not v2.")
for key in [
    "githubAssetValidationStatus",
    "githubPublicationPlanStatus",
    "githubPublicationStatus",
    "postReleaseAuditStatus",
    "localTagExists",
    "nextAction",
]:
    if key not in dashboard:
        raise SystemExit(f"Status dashboard missing Phase 33 key: {key}")

for path in [
    os.path.join(release_dir, "github-release-assets-validation.json"),
    os.path.join(evidence_dir, "github-release-publication-plan.md"),
    os.path.join(evidence_dir, "github-release-publication-plan.json"),
    os.path.join(release_dir, "github-release-publication-summary.json"),
    os.path.join(evidence_dir, "post-release-audit.md"),
    os.path.join(evidence_dir, "post-release-audit.json"),
    os.path.join(release_dir, "release-status-dashboard.json"),
]:
    with open(path, "r", encoding="utf-8") as handle:
        text = handle.read()
    if unsafe.search(text):
        raise SystemExit(f"Phase 33 output contains unsafe content: {os.path.basename(path)}")

for script in [
    "validate-github-release-assets.sh",
    "generate-github-release-publication-plan.sh",
    "publish-github-release.sh",
    "generate-post-release-audit.sh",
]:
    text = open(os.path.join(repo_root, "scripts", script), encoding="utf-8").read()
    if "TOKENFORGE_RELEASE_DIR" not in text or "gh release create" not in text and script == "publish-github-release.sh":
        raise SystemExit(f"Phase 33 script failed static safety check: {script}")

print("Phase 33 summary validation: passed")
PY
}

validate_phase30_summaries() {
  /usr/bin/python3 - "${REPO_ROOT}" "${RELEASE_DIR}" "${EVIDENCE_DIR}" <<'PY'
import json
import os
import re
import sys

repo_root, release_dir, evidence_dir = sys.argv[1:]
manifest = os.path.join(repo_root, "Docs", "release-freeze-manifest-v0.18.0-build18.md")
handoff_md = os.path.join(evidence_dir, "release-operator-handoff.md")
handoff_json = os.path.join(evidence_dir, "release-operator-handoff.json")
audit_json = os.path.join(evidence_dir, "final-privacy-regression-audit.json")
readiness_path = os.path.join(release_dir, "release-readiness-report.json")
record_path = os.path.join(evidence_dir, "final-release-record.json")

secret_value = re.compile(
    r"BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]+|"
    r"password\s*[:=]\s*[^,\s]+|secret\s*[:=]\s*[^,\s]+|token\s*[:=]\s*[^,\s]+|"
    r"/Users/|/private/|~/|tokenforge-approved-locations\.local\.json|"
    r"tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|stack trace|Traceback \(most recent call last\)",
    re.I,
)

for path, label in [
    (manifest, "release freeze manifest"),
    (handoff_md, "release operator handoff markdown"),
    (handoff_json, "release operator handoff JSON"),
    (audit_json, "final privacy regression audit"),
]:
    if not os.path.exists(path):
        raise SystemExit(f"Phase 30 {label} is missing.")

with open(manifest, "r", encoding="utf-8") as handle:
    manifest_text = handle.read()
for required in [
    "Version: 0.18.0",
    "Build: 18",
    "TokenForge-macOS-0.18.0-build18.zip",
    "dry-run only, not ready for public distribution",
    "SIGNING_READY_BUT_IDENTITY_MISSING",
    "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING",
    "DEVELOPER_ID_APPLICATION",
    "APPLE_ID",
    "APPLE_TEAM_ID",
    "APPLE_APP_SPECIFIC_PASSWORD",
    "No local-only sync",
    "No approved-location paths in evidence",
    "No raw sync state in evidence",
    "No secrets in evidence",
]:
    if required not in manifest_text:
        raise SystemExit(f"Phase 30 manifest missing required content: {required}")

for path in [handoff_md, handoff_json]:
    with open(path, "r", encoding="utf-8") as handle:
        text = handle.read()
    if secret_value.search(text):
        raise SystemExit("Release operator handoff contains unsafe content.")
    for required in [
        "scripts/run-credentialed-release.sh --credentialed",
        "scripts/verify-credentialed-release.sh",
        "scripts/generate-final-release-record.sh",
        "scripts/final-privacy-regression-audit.sh",
        "DEVELOPER_ID_APPLICATION",
        "APPLE_APP_SPECIFIC_PASSWORD",
        "dry-run only",
    ]:
        if required not in text:
            raise SystemExit(f"Release operator handoff missing required content: {required}")

with open(audit_json, "r", encoding="utf-8") as handle:
    audit = json.load(handle)
if audit.get("status") != "FINAL_PRIVACY_AUDIT_PASSED":
    raise SystemExit("Final privacy regression audit did not pass.")

def load(path):
    if not os.path.exists(path):
        return {}
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    return data if isinstance(data, dict) else {}

readiness = load(readiness_path)
record = load(record_path)
if readiness.get("releaseStatus") == "readyForDistribution":
    signing = readiness.get("signingSummary", {})
    notary = readiness.get("notarizationSummary", {})
    if signing.get("signingIdentityType") == "adHoc" or notary.get("notarizationStatus") != "NOTARIZATION_SUCCEEDED":
        raise SystemExit("Readiness report overclaims readyForDistribution.")
if record.get("distributionReadiness") == "ready for distribution" and readiness.get("releaseStatus") != "readyForDistribution":
    raise SystemExit("Final release record overclaims ready for distribution.")

print("Phase 30 summary validation: passed")
PY
}

write_release_prep_dry_run_summary() {
  /usr/bin/python3 - "${RELEASE_DIR}" "${EVIDENCE_DIR}" <<'PY'
import datetime
import hashlib
import json
import os
import sys

release_dir, evidence_dir = sys.argv[1:]
package_path = os.environ.get("PACKAGE_PATH") or os.path.join(release_dir, "TokenForge-macOS-0.18.0-build18.zip")

def load(name):
    path = os.path.join(release_dir, name)
    if not os.path.exists(path):
        return {}
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    return data if isinstance(data, dict) else {}

def safe_path(value):
    if value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/"):
        return value
    return "controlled-path"

def sha256(path):
    if not os.path.exists(path):
        return ""
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()

signing = load("signing-status-summary.json")
notary = load("notarization-status-summary.json")
readiness = load("release-readiness-report.json")
signed = signing.get("signedWithDeveloperId") is True and signing.get("signingIdentityType") == "developerId"
if signed:
    raise SystemExit(0)

summary = {
    "schemaVersion": 1,
    "status": "CRED_RELEASE_DRY_RUN_READY",
    "dryRun": True,
    "credentialedRequiredForFinalDistribution": True,
    "currentSigningStatus": signing.get("status", "unknown"),
    "currentNotarizationStatus": notary.get("notarizationStatus", "unknown"),
    "staplingStatus": notary.get("staplingStatus", "notAttempted"),
    "spctlStatus": notary.get("spctlStatus", "notAttempted"),
    "releaseStatus": readiness.get("releaseStatus", "dryRunReady"),
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": sha256(package_path),
    "readinessReportPath": safe_path(os.path.join(release_dir, "release-readiness-report.json")),
    "evidenceBundlePath": safe_path(evidence_dir),
    "nextRequiredCredentialedCommand": "scripts/run-credentialed-release.sh --credentialed",
    "safeFailureReason": "",
    "generatedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}
with open(os.path.join(release_dir, "credentialed-release-summary.json"), "w", encoding="utf-8") as handle:
    json.dump(summary, handle, indent=2, sort_keys=True)
    handle.write("\n")
print("Release prep dry-run credentialed summary: refreshed")
PY
}

for arg in "$@"; do
  case "${arg}" in
    --summary-only)
      SUMMARY_ONLY=true
      ;;
    --help|-h)
      echo "Usage: scripts/release-prep-check.sh [--summary-only]"
      echo "Runs full local validation by default. --summary-only validates existing normalized release summaries without rebuilding."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

if [[ "${SUMMARY_ONLY}" == "true" ]]; then
  echo "TokenForge release prep summary check starting"
  "${SCRIPT_DIR}/validate-release-metadata.sh"
  "${SCRIPT_DIR}/sign-macos-release-candidate.sh"
  "${SCRIPT_DIR}/notarize-macos-release-candidate.sh"
  "${SCRIPT_DIR}/generate-release-readiness-report.sh"
  "${SCRIPT_DIR}/collect-release-evidence.sh"
  "${SCRIPT_DIR}/gatekeeper-qa-helper.sh" --validate-status || true
  "${SCRIPT_DIR}/validate-gatekeeper-qa-status.sh"
  write_release_prep_dry_run_summary
  "${SCRIPT_DIR}/verify-credentialed-release.sh"
  "${SCRIPT_DIR}/generate-final-release-record.sh"
  "${SCRIPT_DIR}/generate-release-readiness-report.sh"
  validate_phase29_summaries
  "${SCRIPT_DIR}/generate-release-operator-handoff.sh"
  "${SCRIPT_DIR}/final-privacy-regression-audit.sh"
  "${SCRIPT_DIR}/validate-release-freeze-manifest.sh"
  "${SCRIPT_DIR}/run-release-machine-finalization.sh" || true
  "${SCRIPT_DIR}/lock-release-evidence.sh" || true
  "${SCRIPT_DIR}/generate-release-readiness-report.sh"
  "${SCRIPT_DIR}/verify-final-distribution-readiness.sh" || true
  "${SCRIPT_DIR}/generate-github-release-draft.sh"
  "${SCRIPT_DIR}/generate-release-bundle-index.sh"
  "${SCRIPT_DIR}/validate-github-release-assets.sh" || true
  "${SCRIPT_DIR}/generate-github-release-publication-plan.sh"
  "${SCRIPT_DIR}/publish-github-release.sh"
  "${SCRIPT_DIR}/generate-post-release-audit.sh"
  "${SCRIPT_DIR}/print-release-status-dashboard.sh" >/dev/null
  TOKENFORGE_RELEASE_TAG_SKIP_PREP_CHECK=true "${SCRIPT_DIR}/prepare-release-tag.sh" >/dev/null
  if [[ "$(/usr/bin/python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get("status",""))' "${RELEASE_DIR}/final-distribution-readiness.json")" != "FINAL_DISTRIBUTION_READY" ]] && TOKENFORGE_RELEASE_TAG_SKIP_PREP_CHECK=true "${SCRIPT_DIR}/prepare-release-tag.sh" --create --yes >/dev/null 2>&1; then
    echo "Dry-run/ad-hoc tag creation unexpectedly succeeded." >&2
    exit 1
  fi
  validate_phase30_summaries
  validate_phase31_summaries
  validate_phase32_summaries
  validate_phase33_summaries
  echo "TokenForge release prep summary check summary"
  echo "Release metadata: passed"
  echo "Signing readiness: passed"
  echo "Notarization readiness: passed"
  echo "Release readiness report: /tmp/tokenforge-release/release-readiness-report.json"
  echo "Release evidence bundle: /tmp/tokenforge-release/evidence"
  echo "Credentialed release verification: /tmp/tokenforge-release/credentialed-release-verification.json"
  echo "Final release record: /tmp/tokenforge-release/evidence/final-release-record.md"
  echo "Release operator handoff: /tmp/tokenforge-release/evidence/release-operator-handoff.md"
  echo "Final privacy regression audit: /tmp/tokenforge-release/evidence/final-privacy-regression-audit.json"
  echo "Release freeze validation: /tmp/tokenforge-release/evidence/release-freeze-validation.json"
  echo "Release machine finalization: /tmp/tokenforge-release/release-machine-finalization-summary.json"
  echo "Evidence lock summary: /tmp/tokenforge-release/evidence/evidence-lock-summary.json"
  echo "Final distribution readiness: /tmp/tokenforge-release/final-distribution-readiness.json"
  echo "GitHub Release draft: /tmp/tokenforge-release/evidence/github-release-draft.md"
  echo "Release bundle index: /tmp/tokenforge-release/evidence/release-bundle-index.md"
  echo "Release status dashboard: /tmp/tokenforge-release/release-status-dashboard.json"
  echo "GitHub asset validation: /tmp/tokenforge-release/github-release-assets-validation.json"
  echo "GitHub publication plan: /tmp/tokenforge-release/evidence/github-release-publication-plan.md"
  echo "GitHub publication summary: /tmp/tokenforge-release/github-release-publication-summary.json"
  echo "Post-release audit: /tmp/tokenforge-release/evidence/post-release-audit.md"
  echo "Gatekeeper QA status validation: passed"
  echo "Result: success"
  exit 0
fi

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable. Set UNITY_PATH to Unity 2022.3.0f1." >&2
  exit 1
fi

echo "TokenForge release prep check starting"
echo "Checks: metadata, EditMode, PlayMode, contract export, macOS build smoke, package smoke, release candidate smoke, signing readiness, notarization readiness, readiness report, evidence bundle, Gatekeeper QA helper"

"${SCRIPT_DIR}/validate-release-metadata.sh"

"${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -runTests \
  -testPlatform editmode \
  -testResults "${EDITMODE_RESULTS}" \
  -logFile "${EDITMODE_LOG}"

"${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -runTests \
  -testPlatform playmode \
  -testResults "${PLAYMODE_RESULTS}" \
  -logFile "${PLAYMODE_LOG}"

"${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -executeMethod TokenForge.Editor.SafeSyncContractBundleExportCommand.Export \
  -contractBundleOutput "${CONTRACT_BUNDLE_OUTPUT}" \
  -quit \
  -logFile "${CONTRACT_EXPORT_LOG}"

"${SCRIPT_DIR}/build-macos-smoke.sh"
"${SCRIPT_DIR}/package-macos-smoke.sh"
"${SCRIPT_DIR}/package-macos-release-candidate.sh"
"${SCRIPT_DIR}/sign-macos-release-candidate.sh"
"${SCRIPT_DIR}/notarize-macos-release-candidate.sh"
"${SCRIPT_DIR}/generate-release-readiness-report.sh"
"${SCRIPT_DIR}/collect-release-evidence.sh"
"${SCRIPT_DIR}/update-gatekeeper-qa-status.sh" init
"${SCRIPT_DIR}/gatekeeper-qa-helper.sh" --validate-status || true
"${SCRIPT_DIR}/validate-gatekeeper-qa-status.sh"
write_release_prep_dry_run_summary
"${SCRIPT_DIR}/verify-credentialed-release.sh"
"${SCRIPT_DIR}/generate-final-release-record.sh"
"${SCRIPT_DIR}/generate-release-readiness-report.sh"
validate_phase29_summaries
"${SCRIPT_DIR}/generate-release-operator-handoff.sh"
"${SCRIPT_DIR}/final-privacy-regression-audit.sh"
"${SCRIPT_DIR}/validate-release-freeze-manifest.sh"
"${SCRIPT_DIR}/run-release-machine-finalization.sh" || true
"${SCRIPT_DIR}/lock-release-evidence.sh" || true
"${SCRIPT_DIR}/generate-release-readiness-report.sh"
"${SCRIPT_DIR}/verify-final-distribution-readiness.sh" || true
"${SCRIPT_DIR}/generate-github-release-draft.sh"
"${SCRIPT_DIR}/generate-release-bundle-index.sh"
"${SCRIPT_DIR}/validate-github-release-assets.sh" || true
"${SCRIPT_DIR}/generate-github-release-publication-plan.sh"
"${SCRIPT_DIR}/publish-github-release.sh"
"${SCRIPT_DIR}/generate-post-release-audit.sh"
"${SCRIPT_DIR}/print-release-status-dashboard.sh" >/dev/null
TOKENFORGE_RELEASE_TAG_SKIP_PREP_CHECK=true "${SCRIPT_DIR}/prepare-release-tag.sh" >/dev/null
if [[ "$(/usr/bin/python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get("status",""))' "${RELEASE_DIR}/final-distribution-readiness.json")" != "FINAL_DISTRIBUTION_READY" ]] && TOKENFORGE_RELEASE_TAG_SKIP_PREP_CHECK=true "${SCRIPT_DIR}/prepare-release-tag.sh" --create --yes >/dev/null 2>&1; then
  echo "Dry-run/ad-hoc tag creation unexpectedly succeeded." >&2
  exit 1
fi
validate_phase30_summaries
validate_phase31_summaries
validate_phase32_summaries
validate_phase33_summaries
TOKENFORGE_CLEAN_INSTALL_RUN_PACKAGE=false "${SCRIPT_DIR}/smoke-clean-install-macos.sh"
"${SCRIPT_DIR}/smoke-update-migration-macos.sh"

echo "TokenForge release prep check summary"
echo "Release metadata: passed"
echo "EditMode results: ${EDITMODE_RESULTS}"
echo "PlayMode results: ${PLAYMODE_RESULTS}"
echo "Contract bundle: ${CONTRACT_BUNDLE_OUTPUT}"
echo "Build/package smoke: passed"
echo "Release candidate package: passed"
echo "Signing readiness: passed"
echo "Notarization readiness: passed"
echo "Release readiness report: /tmp/tokenforge-release/release-readiness-report.json"
echo "Release evidence bundle: /tmp/tokenforge-release/evidence"
echo "Credentialed release verification: /tmp/tokenforge-release/credentialed-release-verification.json"
echo "Final release record: /tmp/tokenforge-release/evidence/final-release-record.md"
echo "Release operator handoff: /tmp/tokenforge-release/evidence/release-operator-handoff.md"
echo "Final privacy regression audit: /tmp/tokenforge-release/evidence/final-privacy-regression-audit.json"
echo "Release freeze validation: /tmp/tokenforge-release/evidence/release-freeze-validation.json"
echo "Release machine finalization: /tmp/tokenforge-release/release-machine-finalization-summary.json"
echo "Evidence lock summary: /tmp/tokenforge-release/evidence/evidence-lock-summary.json"
echo "Final distribution readiness: /tmp/tokenforge-release/final-distribution-readiness.json"
echo "GitHub Release draft: /tmp/tokenforge-release/evidence/github-release-draft.md"
echo "Release bundle index: /tmp/tokenforge-release/evidence/release-bundle-index.md"
echo "Release status dashboard: /tmp/tokenforge-release/release-status-dashboard.json"
echo "GitHub asset validation: /tmp/tokenforge-release/github-release-assets-validation.json"
echo "GitHub publication plan: /tmp/tokenforge-release/evidence/github-release-publication-plan.md"
echo "GitHub publication summary: /tmp/tokenforge-release/github-release-publication-summary.json"
echo "Post-release audit: /tmp/tokenforge-release/evidence/post-release-audit.md"
echo "Gatekeeper QA helper: passed"
echo "Clean install smoke: passed"
echo "Update migration smoke: passed"
echo "Developer ID/notary credentials: not required"
echo "Result: success"

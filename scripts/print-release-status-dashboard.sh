#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
OUTPUT_JSON="${RELEASE_DIR}/release-status-dashboard.json"

mkdir -p "${RELEASE_DIR}" "${EVIDENCE_DIR}"

/usr/bin/python3 - "${OUTPUT_JSON}" "${RELEASE_DIR}" "${EVIDENCE_DIR}" "${PACKAGE_PATH}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys

output_json, release_dir, evidence_dir, package_path, version, build = sys.argv[1:]

UNSAFE = re.compile(
    r"(/Users/|/private/|~/|BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"(password|secret|api[_-]?key|access[_-]?token|refresh[_-]?token)[\"']?\s*[:=]\s*[\"'][^\"'\s]{8,}[\"']|"
    r"tokenforge-approved-locations\.local\.json|tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|raw server|prompt:|response:|Traceback \(most recent call last\)|stack trace|"
    r"xattr -d com\.apple\.quarantine|spctl\s+--master-disable)",
    re.I,
)


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def safe_path(value):
    if value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/"):
        return value
    return "controlled-path"


def load_json(path):
    if not os.path.exists(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data if isinstance(data, dict) else {}
    except Exception:
        return {}


def sha256(path):
    if not os.path.isfile(path):
        return ""
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git_bool(args):
    try:
        return subprocess.run(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False).returncode == 0
    except Exception:
        return False


readiness = load_json(os.path.join(release_dir, "release-readiness-report.json"))
verification = load_json(os.path.join(release_dir, "credentialed-release-verification.json"))
finalization = load_json(os.path.join(release_dir, "release-machine-finalization-summary.json"))
final_distribution = load_json(os.path.join(release_dir, "final-distribution-readiness.json"))
lock = load_json(os.path.join(evidence_dir, "evidence-lock-summary.json"))
assets = load_json(os.path.join(release_dir, "github-release-assets-validation.json"))
plan = load_json(os.path.join(evidence_dir, "github-release-publication-plan.json"))
publication = load_json(os.path.join(release_dir, "github-release-publication-summary.json"))
post_audit = load_json(os.path.join(evidence_dir, "post-release-audit.json"))
qa = load_json(os.path.join(evidence_dir, "gatekeeper-qa-status.json"))
signing = readiness.get("signingSummary", {}) if isinstance(readiness.get("signingSummary"), dict) else load_json(os.path.join(release_dir, "signing-status-summary.json"))
notary = readiness.get("notarizationSummary", {}) if isinstance(readiness.get("notarizationSummary"), dict) else load_json(os.path.join(release_dir, "notarization-status-summary.json"))

final_status = final_distribution.get("status", "missing")
qa_status = qa.get("qaOverallStatus", qa.get("overallStatus", readiness.get("qaStatusSummary", {}).get("overallStatus", "missing")))
tag_readiness = "allowed" if final_distribution.get("tagAllowed") is True else "blocked"
tag = f"v{version}-build{build}"
local_tag_exists = git_bool(["git", "rev-parse", "-q", "--verify", f"refs/tags/{tag}"])
asset_status = assets.get("status", "missing")
plan_status = plan.get("status", "missing")
publication_status = publication.get("status", "missing")
post_audit_status = post_audit.get("status", "missing")
if publication_status in {"GITHUB_RELEASE_PUBLISHED", "GITHUB_RELEASE_ALREADY_EXISTS"} and post_audit_status == "POST_RELEASE_AUDIT_RELEASE_COMPLETE":
    next_action = "release complete"
elif publication_status in {"GITHUB_RELEASE_PUBLISHED", "GITHUB_RELEASE_ALREADY_EXISTS"}:
    next_action = "verify release"
elif final_status == "FINAL_DISTRIBUTION_DRY_RUN_ONLY":
    next_action = "run credentialed finalization"
elif final_status == "FINAL_DISTRIBUTION_PENDING_QA" or qa_status in {"inProgress", "notStarted", "missing"}:
    next_action = "complete QA"
elif final_status == "FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK":
    next_action = "lock evidence"
elif final_status == "FINAL_DISTRIBUTION_READY" and not local_tag_exists:
    next_action = "create tag"
elif final_status == "FINAL_DISTRIBUTION_READY" and asset_status != "GITHUB_RELEASE_ASSETS_READY":
    next_action = "validate GitHub assets"
elif final_status == "FINAL_DISTRIBUTION_READY" and plan_status != "GITHUB_RELEASE_PUBLICATION_PLAN_READY":
    next_action = "publish GitHub Release"
elif final_status == "FINAL_DISTRIBUTION_READY":
    next_action = "publish GitHub Release"
else:
    next_action = "run credentialed finalization"

dashboard = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.releaseStatusDashboard.v2",
    "version": version,
    "build": build,
    "releaseCandidatePath": safe_path(package_path),
    "currentSha256": sha256(package_path) or readiness.get("releaseCandidateSha256", ""),
    "signingStatus": signing.get("status", "unknown"),
    "notarizationStatus": notary.get("notarizationStatus", "unknown"),
    "staplingStatus": notary.get("staplingStatus", "notAttempted"),
    "spctlStatus": notary.get("spctlStatus", "notAttempted"),
    "readinessReleaseStatus": readiness.get("releaseStatus", "missing"),
    "verificationStatus": verification.get("status", "missing"),
    "releaseMachineFinalizationStatus": finalization.get("status", "missing"),
    "qaOverallStatus": qa_status,
    "evidenceLockStatus": lock.get("status", "missing"),
    "finalDistributionStatus": final_status,
    "githubAssetValidationStatus": asset_status,
    "githubPublicationPlanStatus": plan_status,
    "githubPublicationStatus": publication_status,
    "postReleaseAuditStatus": post_audit_status,
    "suggestedTag": tag,
    "localTagExists": local_tag_exists,
    "tagReadiness": tag_readiness,
    "nextAction": next_action,
    "generatedAt": utc_now(),
}

text = json.dumps(dashboard, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("Release status dashboard JSON contains unsafe content.")
with open(output_json, "w", encoding="utf-8") as handle:
    handle.write(text)

lines = [
    "TokenForge release status dashboard",
    f"Version/build: {version} build {build}",
    f"Release candidate: {dashboard['releaseCandidatePath']}",
    f"Current SHA-256: {dashboard['currentSha256'] or 'not available'}",
    f"Signing status: {dashboard['signingStatus']}",
    f"Notarization status: {dashboard['notarizationStatus']}",
    f"Stapling status: {dashboard['staplingStatus']}",
    f"spctl status: {dashboard['spctlStatus']}",
    f"Readiness releaseStatus: {dashboard['readinessReleaseStatus']}",
    f"Verification status: {dashboard['verificationStatus']}",
    f"Release-machine finalization status: {dashboard['releaseMachineFinalizationStatus']}",
    f"QA overall: {dashboard['qaOverallStatus']}",
    f"Evidence lock status: {dashboard['evidenceLockStatus']}",
    f"Final distribution status: {dashboard['finalDistributionStatus']}",
    f"GitHub asset validation status: {dashboard['githubAssetValidationStatus']}",
    f"GitHub publication plan status: {dashboard['githubPublicationPlanStatus']}",
    f"GitHub publication status: {dashboard['githubPublicationStatus']}",
    f"Post-release audit status: {dashboard['postReleaseAuditStatus']}",
    f"Suggested tag: {dashboard['suggestedTag']}",
    f"Local tag exists: {str(dashboard['localTagExists']).lower()}",
    f"Tag readiness: {dashboard['tagReadiness']}",
    f"Next action: {dashboard['nextAction']}",
    f"Dashboard JSON: {safe_path(output_json)}",
]
output = "\n".join(lines)
if UNSAFE.search(output):
    raise SystemExit("Release status dashboard output contains unsafe content.")
print(output)
PY

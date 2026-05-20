#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
FINAL_DISTRIBUTION="${RELEASE_DIR}/final-distribution-readiness.json"
ASSET_VALIDATION="${RELEASE_DIR}/github-release-assets-validation.json"
PLAN_JSON="${EVIDENCE_DIR}/github-release-publication-plan.json"
PLAN_MD="${EVIDENCE_DIR}/github-release-publication-plan.md"
PUBLICATION_SUMMARY="${RELEASE_DIR}/github-release-publication-summary.json"
READINESS_REPORT="${RELEASE_DIR}/release-readiness-report.json"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
BUNDLE_INDEX="${EVIDENCE_DIR}/release-bundle-index.json"
EVIDENCE_LOCK="${EVIDENCE_DIR}/evidence-lock-summary.json"
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
AUDIT_MD="${EVIDENCE_DIR}/post-release-audit.md"
AUDIT_JSON="${EVIDENCE_DIR}/post-release-audit.json"

mkdir -p "${RELEASE_DIR}" "${EVIDENCE_DIR}"

/usr/bin/python3 - "${AUDIT_MD}" "${AUDIT_JSON}" "${PACKAGE_PATH}" "${FINAL_DISTRIBUTION}" "${ASSET_VALIDATION}" "${PLAN_JSON}" "${PLAN_MD}" "${PUBLICATION_SUMMARY}" "${READINESS_REPORT}" "${FINAL_RECORD_JSON}" "${BUNDLE_INDEX}" "${EVIDENCE_LOCK}" "${QA_STATUS}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys

(
    audit_md,
    audit_json,
    package_path,
    final_distribution_path,
    validation_path,
    plan_json_path,
    plan_md_path,
    publication_path,
    readiness_path,
    record_path,
    bundle_path,
    lock_path,
    qa_path,
    version,
    build,
) = sys.argv[1:]

UNSAFE = re.compile(
    r"(/Users/|/private/|~/|BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"(password|secret|api[_-]?key|access[_-]?token|refresh[_-]?token)[\"']?\s*[:=]\s*[\"'][^\"'\s]{8,}[\"']|"
    r"tokenforge-approved-locations\.local\.json|tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|raw server|prompt:|response:|Traceback \(most recent call last\)|"
    r"stack trace|Exception:|source snippet|```[a-zA-Z]*\n(class|namespace|public|using)\s|"
    r"\bcommit\s+[0-9a-f]{7,40}\b|xattr -d com\.apple\.quarantine|spctl\s+--master-disable)",
    re.I,
)


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def load_json(path):
    if not os.path.exists(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data if isinstance(data, dict) else {}
    except Exception:
        return {}


def safe_path(value):
    if isinstance(value, str) and (value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/")):
        return value
    return "controlled-path" if value else ""


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


final_distribution = load_json(final_distribution_path)
validation = load_json(validation_path)
plan = load_json(plan_json_path)
publication = load_json(publication_path)
readiness = load_json(readiness_path)
record = load_json(record_path)
bundle = load_json(bundle_path)
lock = load_json(lock_path)
qa = load_json(qa_path)
privacy = readiness.get("privacyAssertions", {}) if isinstance(readiness.get("privacyAssertions"), dict) else {}

tag = plan.get("tag") or f"v{version}-build{build}"
tag_exists = git_bool(["git", "rev-parse", "-q", "--verify", f"refs/tags/{tag}"])
tag_points_to_release_checkout = tag_exists and git_bool(["git", "merge-base", "--is-ancestor", f"refs/tags/{tag}", "HEAD"]) and git_bool(["git", "merge-base", "--is-ancestor", "HEAD", f"refs/tags/{tag}"])
tag_pushed_verifiable = False
if tag_exists:
    tag_pushed_verifiable = git_bool(["git", "ls-remote", "--exit-code", "--tags", "origin", tag])

publication_status = publication.get("status", "notRun")
published = publication_status in {"GITHUB_RELEASE_PUBLISHED", "GITHUB_RELEASE_ALREADY_EXISTS"}
qa_status = qa.get("qaOverallStatus", qa.get("overallStatus", readiness.get("qaStatusSummary", {}).get("overallStatus", "missing")))
final_status = final_distribution.get("status", "missing")
asset_status = validation.get("status", "missing")
lock_status = lock.get("status", "missing")
release_sha = validation.get("releaseCandidateSha256") or final_distribution.get("releaseCandidateSha256") or record.get("releaseCandidateSha256") or bundle.get("releaseCandidateSha256") or sha256(package_path)
evidence_path = validation.get("evidenceArchivePath") or lock.get("evidenceArchivePath", "")
evidence_sha = validation.get("evidenceArchiveSha256") or lock.get("evidenceArchiveSha256", "")

if publication_status in {"GITHUB_RELEASE_PUBLISHED", "GITHUB_RELEASE_ALREADY_EXISTS"} and tag_exists and final_status == "FINAL_DISTRIBUTION_READY" and asset_status == "GITHUB_RELEASE_ASSETS_READY":
    follow_up = "release complete"
elif publication_status in {"GITHUB_RELEASE_PUBLISHED", "GITHUB_RELEASE_ALREADY_EXISTS"}:
    follow_up = "verify published release"
elif final_status != "FINAL_DISTRIBUTION_READY":
    follow_up = "run credentialed release" if final_status in {"FINAL_DISTRIBUTION_DRY_RUN_ONLY", "missing"} else "complete QA" if final_status == "FINAL_DISTRIBUTION_PENDING_QA" else "blocked with reason"
elif qa_status != "passed":
    follow_up = "complete QA"
elif lock_status != "EVIDENCE_LOCK_READY":
    follow_up = "lock evidence"
elif not tag_exists:
    follow_up = "create tag"
elif asset_status != "GITHUB_RELEASE_ASSETS_READY":
    follow_up = "validate GitHub assets"
elif publication_status in {"GITHUB_RELEASE_DRY_RUN_READY", "GITHUB_RELEASE_DRY_RUN_BLOCKED", "notRun", ""}:
    follow_up = "publish GitHub Release"
else:
    follow_up = "verify published release"

known_limitations = readiness.get("knownLimitations", [])
if not isinstance(known_limitations, list):
    known_limitations = []
if not published:
    known_limitations = list(known_limitations) + ["GitHub Release has not been published."]

audit_status = "POST_RELEASE_AUDIT_RELEASE_COMPLETE" if follow_up == "release complete" else "POST_RELEASE_AUDIT_VERIFY_RELEASE" if published else "POST_RELEASE_AUDIT_NOT_PUBLISHED"

audit = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.postReleaseAudit.v1",
    "status": audit_status,
    "version": version,
    "build": build,
    "tag": tag,
    "tagExistsLocally": tag_exists,
    "tagPointsToExpectedReleaseCommit": tag_points_to_release_checkout,
    "tagPushedVerifiableWithoutSecrets": tag_pushed_verifiable,
    "releaseCandidateFilename": os.path.basename(package_path),
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": release_sha,
    "evidenceArchiveFilename": os.path.basename(evidence_path) if evidence_path else "",
    "evidenceArchivePath": safe_path(evidence_path) if evidence_path else "",
    "evidenceArchiveSha256": evidence_sha if evidence_path else "",
    "publicationStatus": publication_status,
    "published": published,
    "publishedAt": publication.get("publishedAt", "") if published else "",
    "githubReleaseUrl": publication.get("releaseUrl", "") if published else "",
    "finalDistributionStatus": final_status,
    "assetValidationStatus": asset_status,
    "qaOverallStatus": qa_status,
    "evidenceLockStatus": lock_status,
    "privacyAssertions": {
        "noLocalOnlyFilesInPackage": privacy.get("noLocalOnlyFilesInPackage") is True,
        "noApprovedLocationPathsInEvidence": privacy.get("noApprovedLocationPathsInEvidence") is True,
        "noSecretsInEvidence": privacy.get("noSecretsInEvidence") is True,
        "noRawSyncStateInEvidence": privacy.get("noRawSyncStateInEvidence") is True,
        "noBackgroundSyncAdded": privacy.get("noBackgroundSyncAdded") is True,
    },
    "knownLimitations": known_limitations,
    "finalFollowUpAction": follow_up,
    "publicationSummaryPath": safe_path(publication_path) if os.path.exists(publication_path) else "",
    "publicationPlanPath": safe_path(plan_md_path) if os.path.exists(plan_md_path) else "",
    "generatedAt": utc_now(),
}

json_text = json.dumps(audit, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(json_text):
    raise SystemExit("Post-release audit JSON contains unsafe content.")
with open(audit_json, "w", encoding="utf-8") as handle:
    handle.write(json_text)

def bool_text(value):
    return "true" if value else "false"

limitations = known_limitations or ["None recorded."]
md_lines = [
    "# TokenForge Post-Release Audit",
    "",
    f"- Version/build: {version} build {build}",
    f"- Tag: `{tag}`",
    f"- Tag exists locally: {bool_text(tag_exists)}",
    f"- Tag points to expected release checkout: {bool_text(tag_points_to_release_checkout)}",
    f"- Tag pushed verifiable without secrets: {bool_text(tag_pushed_verifiable)}",
    f"- Release asset: `{os.path.basename(package_path)}`",
    f"- Release asset SHA-256: `{release_sha or 'not available'}`",
    f"- Evidence archive: `{os.path.basename(evidence_path) if evidence_path else 'not attached'}`",
    f"- Evidence archive SHA-256: `{evidence_sha if evidence_path else 'not available'}`",
    f"- Publication status: `{publication_status}`",
    f"- Audit status: `{audit_status}`",
    f"- Published: {bool_text(published)}",
    f"- Publication timestamp: `{audit['publishedAt'] or 'not available'}`",
    f"- GitHub Release URL: `{audit['githubReleaseUrl'] or 'not available'}`",
    f"- Final distribution status: `{final_status}`",
    f"- QA status: `{qa_status}`",
    f"- Final follow-up action: `{follow_up}`",
    "",
    "## Privacy Assertions",
]
for key, value in audit["privacyAssertions"].items():
    md_lines.append(f"- {key}: {bool_text(value)}")
md_lines.extend(["", "## Known Limitations"])
md_lines.extend(f"- {item}" for item in limitations)
if not published:
    md_lines.extend(["", "No actual GitHub Release publication is claimed by this audit."])
md_text = "\n".join(md_lines) + "\n"
if UNSAFE.search(md_text):
    raise SystemExit("Post-release audit markdown contains unsafe content.")
with open(audit_md, "w", encoding="utf-8") as handle:
    handle.write(md_text)

print("TokenForge post-release audit")
print(f"Publication status: {publication_status}")
print(f"Published: {bool_text(published)}")
print(f"Final follow-up action: {follow_up}")
print(f"Markdown: {safe_path(audit_md)}")
print(f"JSON: {safe_path(audit_json)}")
PY

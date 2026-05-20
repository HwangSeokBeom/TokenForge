#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
INDEX_JSON="${EVIDENCE_DIR}/release-bundle-index.json"
INDEX_MD="${EVIDENCE_DIR}/release-bundle-index.md"

mkdir -p "${EVIDENCE_DIR}"

/usr/bin/python3 - "${INDEX_JSON}" "${INDEX_MD}" "${RELEASE_DIR}" "${EVIDENCE_DIR}" "${PACKAGE_PATH}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

index_json, index_md, release_dir, evidence_dir, package_path, version, build = sys.argv[1:]

UNSAFE = re.compile(
    r"(/Users/|/private/|~/|BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"(password|secret|api[_-]?key|access[_-]?token|refresh[_-]?token)[\"']?\s*[:=]\s*[\"'][^\"'\s]{8,}[\"']|"
    r"tokenforge-approved-locations\.local\.json|tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|raw server|prompt:|response:|Traceback \(most recent call last\)|stack trace|"
    r"\bcommit\s+[0-9a-f]{7,40}\b|xattr -d com\.apple\.quarantine|spctl\s+--master-disable)",
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


final_distribution = load_json(os.path.join(release_dir, "final-distribution-readiness.json"))
lock = load_json(os.path.join(evidence_dir, "evidence-lock-summary.json"))
readiness = load_json(os.path.join(release_dir, "release-readiness-report.json"))
status = final_distribution.get("status", "FINAL_DISTRIBUTION_BLOCKED")
tag_allowed = final_distribution.get("tagAllowed") is True
tag = f"v{version}-build{build}"


def artifact(label, path, category, required=False, optional=False, internal=False, sha_value=""):
    exists = bool(path and os.path.exists(path))
    marker = "missing"
    if exists:
        if required:
            marker = "requiredForDistribution"
        elif optional:
            marker = "optionalEvidence"
        else:
            marker = "internalOnly" if internal else "optionalEvidence"
    return {
        "label": label,
        "path": safe_path(path) if path else "",
        "filename": os.path.basename(path) if path else "",
        "exists": exists,
        "classification": marker,
        "requiredForDistribution": bool(required and exists),
        "optionalEvidence": bool(optional and exists),
        "internalOnly": bool(internal and exists),
        "missing": not exists,
        "sha256": sha_value or (sha256(path) if exists and os.path.isfile(path) else ""),
        "category": category,
    }


artifacts = [
    artifact("Release candidate zip", package_path, "artifact", required=True),
    artifact("Final release record", os.path.join(evidence_dir, "final-release-record.md"), "record", required=True),
    artifact("GitHub Release draft markdown", os.path.join(evidence_dir, "github-release-draft.md"), "githubRelease", optional=True),
    artifact("GitHub Release draft JSON", os.path.join(evidence_dir, "github-release-draft.json"), "githubRelease", internal=True),
    artifact("Readiness report", os.path.join(release_dir, "release-readiness-report.json"), "summary", internal=True),
    artifact("Final distribution readiness summary", os.path.join(release_dir, "final-distribution-readiness.json"), "summary", internal=True),
    artifact("Credentialed release summary", os.path.join(release_dir, "credentialed-release-summary.json"), "summary", internal=True),
    artifact("Credentialed verification summary", os.path.join(release_dir, "credentialed-release-verification.json"), "summary", internal=True),
    artifact("Signing summary", os.path.join(release_dir, "signing-status-summary.json"), "summary", internal=True),
    artifact("Notarization summary", os.path.join(release_dir, "notarization-status-summary.json"), "summary", internal=True),
    artifact("Release-machine finalization summary", os.path.join(release_dir, "release-machine-finalization-summary.json"), "summary", internal=True),
    artifact("Gatekeeper QA status", os.path.join(evidence_dir, "gatekeeper-qa-status.json"), "qa", internal=True),
    artifact("Final privacy audit", os.path.join(evidence_dir, "final-privacy-regression-audit.json"), "privacy", internal=True),
    artifact("Freeze validation", os.path.join(evidence_dir, "release-freeze-validation.json"), "freeze", internal=True),
]
if lock.get("status") == "EVIDENCE_LOCK_READY":
    artifacts.append(artifact("Evidence archive", lock.get("evidenceArchivePath", ""), "evidenceArchive", optional=True, sha_value=lock.get("evidenceArchiveSha256", "")))
else:
    artifacts.append(artifact("Evidence archive", lock.get("evidenceArchivePath", ""), "evidenceArchive", optional=True))

summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.releaseBundleIndex.v1",
    "version": version,
    "build": build,
    "finalDistributionStatus": status,
    "distributionReady": status == "FINAL_DISTRIBUTION_READY",
    "releaseCandidateSha256": final_distribution.get("releaseCandidateSha256") or readiness.get("releaseCandidateSha256") or sha256(package_path),
    "evidenceArchivePath": lock.get("evidenceArchivePath", "") if lock.get("status") == "EVIDENCE_LOCK_READY" else "",
    "evidenceArchiveSha256": lock.get("evidenceArchiveSha256", "") if lock.get("status") == "EVIDENCE_LOCK_READY" else "",
    "tagRecommendation": tag if tag_allowed else f"{tag} blocked until final distribution readiness is ready",
    "artifacts": artifacts,
    "generatedAt": utc_now(),
}

json_text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(json_text):
    raise SystemExit("Release bundle index JSON contains unsafe content.")
with open(index_json, "w", encoding="utf-8") as handle:
    handle.write(json_text)

lines = [
    "# TokenForge Release Bundle Index",
    "",
    f"- Version/build: {version} build {build}",
    f"- Final distribution status: {status}",
    f"- Distribution ready: {str(summary['distributionReady']).lower()}",
    f"- Release candidate SHA-256: {summary['releaseCandidateSha256'] or 'not available'}",
    f"- Tag recommendation: {summary['tagRecommendation']}",
    "",
    "If final distribution status is not FINAL_DISTRIBUTION_READY, this index is an internal preparation bundle only.",
    "",
    "## Artifacts",
]
for item in artifacts:
    lines.append(f"- {item['label']}: {item['classification']} - {item['filename'] or 'missing'}" + (f" - SHA-256 {item['sha256']}" if item.get("sha256") else ""))
md_text = "\n".join(lines) + "\n"
if UNSAFE.search(md_text):
    raise SystemExit("Release bundle index markdown contains unsafe content.")
with open(index_md, "w", encoding="utf-8") as handle:
    handle.write(md_text)

print("TokenForge release bundle index")
print(f"Status: {status}")
print(f"JSON: {safe_path(index_json)}")
print(f"Markdown: {safe_path(index_md)}")
print(f"Tag recommendation: {summary['tagRecommendation']}")
PY

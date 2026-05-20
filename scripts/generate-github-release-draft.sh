#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
FINAL_DISTRIBUTION="${RELEASE_DIR}/final-distribution-readiness.json"
EVIDENCE_LOCK="${EVIDENCE_DIR}/evidence-lock-summary.json"
FREEZE_MANIFEST="${TOKENFORGE_RELEASE_FREEZE_MANIFEST:-Docs/release-freeze-manifest-v${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.md}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
DRAFT_MD="${EVIDENCE_DIR}/github-release-draft.md"
DRAFT_JSON="${EVIDENCE_DIR}/github-release-draft.json"

mkdir -p "${EVIDENCE_DIR}"

if [[ ! -f "${FINAL_DISTRIBUTION}" ]]; then
  PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${BASH_SOURCE%/*}/verify-final-distribution-readiness.sh" >/dev/null || true
fi

/usr/bin/python3 - "${DRAFT_MD}" "${DRAFT_JSON}" "${PACKAGE_PATH}" "${FINAL_RECORD_JSON}" "${FINAL_DISTRIBUTION}" "${EVIDENCE_LOCK}" "${READINESS_REPORT}" "${FREEZE_MANIFEST}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

draft_md, draft_json, package_path, record_path, final_distribution_path, lock_path, readiness_path, freeze_manifest_path, version, build = sys.argv[1:]

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


record = load_json(record_path)
final_distribution = load_json(final_distribution_path)
lock = load_json(lock_path)
readiness = load_json(readiness_path)
notary = readiness.get("notarizationSummary", {}) if isinstance(readiness.get("notarizationSummary"), dict) else {}

status = final_distribution.get("status", "FINAL_DISTRIBUTION_BLOCKED")
sha = final_distribution.get("releaseCandidateSha256") or record.get("releaseCandidateSha256") or readiness.get("releaseCandidateSha256") or sha256(package_path)
filename = os.path.basename(package_path)
tag = f"v{version}-build{build}"
title = f"TokenForge macOS {version} build {build}"
notarized = notary.get("notarizationStatus") == "NOTARIZATION_SUCCEEDED"
ready = status == "FINAL_DISTRIBUTION_READY"

status_labels = {
    "FINAL_DISTRIBUTION_READY": "ready for distribution",
    "FINAL_DISTRIBUTION_DRY_RUN_ONLY": "dry-run only; not public distribution-ready",
    "FINAL_DISTRIBUTION_PENDING_QA": "pending Gatekeeper QA; not public distribution-ready",
    "FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK": "pending final evidence lock; not public distribution-ready",
    "FINAL_DISTRIBUTION_BLOCKED": "blocked; not public distribution-ready",
}
release_status = status_labels.get(status, "blocked; not public distribution-ready")

install_notes = (
    "Download the release zip, verify the SHA-256 checksum below, unzip it, move TokenForge.app to Applications, and launch it normally."
    if ready
    else "This draft is for internal release preparation only. The current artifact is not public distribution-ready."
)
notary_note = (
    "Developer ID notarization, stapling, and spctl assessment are complete according to final evidence."
    if notarized and ready
    else "Developer ID notarization is not claimed complete for this draft."
)

asset_checklist = [
    {"name": filename, "type": "release candidate zip", "requiredForDistribution": True, "sha256": sha},
    {"name": "SHA-256 checksum", "type": "checksum", "requiredForDistribution": True, "sha256": sha},
    {"name": "final-release-record.md", "type": "final release record", "requiredForDistribution": True, "path": safe_path(os.path.join(os.path.dirname(draft_md), "final-release-record.md"))},
]
if lock.get("status") == "EVIDENCE_LOCK_READY":
    asset_checklist.append({
        "name": os.path.basename(lock.get("evidenceArchivePath", "release-evidence.zip")) or "release-evidence.zip",
        "type": "release evidence archive",
        "requiredForDistribution": False,
        "path": safe_path(lock.get("evidenceArchivePath", "")) if lock.get("evidenceArchivePath") else "",
        "sha256": lock.get("evidenceArchiveSha256", ""),
    })

known_limitations = [
    "No raw recovery by design.",
    "No background sync, retry, tombstone, or conflict processing.",
    "Safe Sync schema v1.",
    notary_note,
]

public_md = f"""# {title}

Suggested tag: `{tag}`

Artifact: `{filename}`

SHA-256: `{sha or 'not available'}`

Release status: {release_status}

## Release Notes

- MVP macOS release candidate.
- Safe Sync privacy-first local-only state.
- Explicit conflict, retry, and tombstone actions.
- Release hardening and Gatekeeper QA.

## Installation Notes

{install_notes}

## Known Limitations

""" + "\n".join(f"- {item}" for item in known_limitations) + """

## Asset Checklist

""" + "\n".join(f"- {item['name']}: {item['type']}" + (f" SHA-256 `{item.get('sha256')}`" if item.get("sha256") else "") for item in asset_checklist) + "\n"

metadata = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.githubReleaseDraft.v1",
    "releaseTitle": title,
    "suggestedTag": tag,
    "artifactFilename": filename,
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": sha,
    "releaseStatus": release_status,
    "finalDistributionStatus": status,
    "notarizationStatus": notary.get("notarizationStatus", "NOTARIZATION_NOT_ATTEMPTED"),
    "installationNotesIncludedForPublicDistribution": ready,
    "assetChecklist": asset_checklist,
    "publicMarkdownPath": safe_path(draft_md),
    "freezeManifest": "Docs/release-freeze-manifest-v0.18.0-build18.md",
    "generatedAt": utc_now(),
}

json_text = json.dumps(metadata, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(public_md) or UNSAFE.search(json_text):
    raise SystemExit("GitHub Release draft contains unsafe content.")
with open(draft_md, "w", encoding="utf-8") as handle:
    handle.write(public_md)
with open(draft_json, "w", encoding="utf-8") as handle:
    handle.write(json_text)

print("TokenForge GitHub Release draft")
print(f"Status: {status}")
print(f"Markdown: {safe_path(draft_md)}")
print(f"JSON: {safe_path(draft_json)}")
print(f"Suggested tag: {tag}")
print(f"SHA-256: {sha or 'not available'}")
PY

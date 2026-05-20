#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
FINAL_DISTRIBUTION="${RELEASE_DIR}/final-distribution-readiness.json"
DRAFT_JSON="${EVIDENCE_DIR}/github-release-draft.json"
DRAFT_MD="${EVIDENCE_DIR}/github-release-draft.md"
BUNDLE_INDEX="${EVIDENCE_DIR}/release-bundle-index.json"
BUNDLE_INDEX_MD="${EVIDENCE_DIR}/release-bundle-index.md"
EVIDENCE_LOCK="${EVIDENCE_DIR}/evidence-lock-summary.json"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
FINAL_RECORD_MD="${EVIDENCE_DIR}/final-release-record.md"
OUTPUT_JSON="${RELEASE_DIR}/github-release-assets-validation.json"

mkdir -p "${RELEASE_DIR}" "${EVIDENCE_DIR}"

/usr/bin/python3 - "${OUTPUT_JSON}" "${PACKAGE_PATH}" "${READINESS_REPORT}" "${FINAL_DISTRIBUTION}" "${DRAFT_JSON}" "${DRAFT_MD}" "${BUNDLE_INDEX}" "${BUNDLE_INDEX_MD}" "${EVIDENCE_LOCK}" "${FINAL_RECORD_JSON}" "${FINAL_RECORD_MD}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys
import zipfile

(
    output_json,
    package_path,
    readiness_path,
    final_distribution_path,
    draft_json_path,
    draft_md_path,
    bundle_path,
    bundle_md_path,
    lock_path,
    record_json_path,
    record_md_path,
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
FORBIDDEN_ZIP_ENTRY = re.compile(
    r"(^|/)tokenforge-approved-locations\.local\.json$|(^|/)tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json$|"
    r"(^|/)\.env$|\.log$|\.p8$|\.p12$|private[_-]?key|credential-values?|raw[ _-]?sync[ _-]?payload|"
    r"tokenforge-session|tokenforge-token|retry[ _-]?backup|recovery[ _-]?sync[ _-]?state",
    re.I,
)


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def safe_path(value):
    if isinstance(value, str) and (value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/")):
        return value
    return "controlled-path" if value else ""


def load_json(path):
    if not os.path.exists(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data if isinstance(data, dict) else {}
    except Exception:
        return {}


def read_text(path):
    if not os.path.exists(path):
        return ""
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as handle:
            return handle.read()
    except OSError:
        return ""


def sha256(path):
    if not os.path.isfile(path):
        return ""
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def scan_zip(path, label):
    failures = []
    if not os.path.exists(path):
        failures.append(f"{label} is missing.")
        return failures
    if not zipfile.is_zipfile(path):
        failures.append(f"{label} is not a valid zip archive.")
        return failures
    with zipfile.ZipFile(path, "r") as archive:
        for entry in archive.infolist():
            name = entry.filename
            if FORBIDDEN_ZIP_ENTRY.search(name) or UNSAFE.search(name):
                failures.append(f"{label} contains unsafe entry.")
                continue
            if entry.file_size > 2 * 1024 * 1024:
                continue
            try:
                data = archive.read(entry, pwd=None)
            except Exception:
                failures.append(f"{label} contains unreadable entry: {os.path.basename(name) or 'unnamed entry'}.")
                continue
            if b"\0" in data[:4096]:
                continue
            sample = data[:4096]
            if sample:
                printable = sum(1 for byte in sample if byte in b"\n\r\t" or 32 <= byte <= 126)
                if printable / max(len(sample), 1) < 0.85:
                    continue
            try:
                text = data.decode("utf-8", errors="ignore")
            except Exception:
                text = ""
            if text and UNSAFE.search(text):
                failures.append(f"{label} contains unsafe content.")
    return failures


readiness = load_json(readiness_path)
final_distribution = load_json(final_distribution_path)
draft = load_json(draft_json_path)
bundle = load_json(bundle_path)
lock = load_json(lock_path)
record = load_json(record_json_path)

expected_zip_name = f"TokenForge-macOS-{version}-build{build}.zip"
expected_evidence_name = f"TokenForge-{version}-build{build}-release-evidence.zip"
package_sha = sha256(package_path)
final_status = final_distribution.get("status", "missing")
lock_status = lock.get("status", "missing")
blocked = []
warnings = []

if os.path.basename(package_path) != expected_zip_name:
    blocked.append(f"Release candidate filename must be {expected_zip_name}.")
if not package_sha:
    blocked.append("Release candidate zip is missing.")
else:
    blocked.extend(scan_zip(package_path, "Release candidate zip"))

sha_sources = {
    "final distribution readiness": final_distribution.get("releaseCandidateSha256", ""),
    "readiness report": readiness.get("releaseCandidateSha256", ""),
    "final release record": record.get("releaseCandidateSha256", ""),
    "GitHub Release draft": draft.get("releaseCandidateSha256", ""),
    "release bundle index": bundle.get("releaseCandidateSha256", ""),
}
for label, value in sha_sources.items():
    if not value:
        blocked.append(f"Missing release candidate SHA-256 in {label}.")
    elif package_sha and value != package_sha:
        blocked.append(f"SHA-256 mismatch between release candidate zip and {label}.")

record_md = read_text(record_md_path)
if package_sha and record_md and package_sha not in record_md:
    blocked.append("Final release record markdown does not contain the release candidate SHA-256.")

if draft.get("finalDistributionStatus") != final_status:
    blocked.append("GitHub Release draft and final distribution status disagree.")
if draft.get("finalDistributionStatus") != "FINAL_DISTRIBUTION_READY" and draft.get("installationNotesIncludedForPublicDistribution") is True:
    blocked.append("GitHub Release draft overclaims public distribution readiness.")
if final_status != "FINAL_DISTRIBUTION_READY" and (
    draft.get("installationNotesIncludedForPublicDistribution") is True
    or bundle.get("distributionReady") is True
):
    blocked.append("Draft or bundle index overclaims distribution readiness.")
if final_status == "FINAL_DISTRIBUTION_READY" and draft.get("installationNotesIncludedForPublicDistribution") is not True:
    blocked.append("GitHub Release draft is not marked public-ready even though final distribution is ready.")
if bundle.get("distributionReady") != (final_status == "FINAL_DISTRIBUTION_READY"):
    blocked.append("Release bundle index distributionReady is inconsistent with final distribution status.")

evidence_path = lock.get("evidenceArchivePath") or bundle.get("evidenceArchivePath", "")
evidence_sha = lock.get("evidenceArchiveSha256") or bundle.get("evidenceArchiveSha256", "")
evidence_ready = lock_status == "EVIDENCE_LOCK_READY" or bool(bundle.get("evidenceArchivePath"))
if evidence_ready:
    if not evidence_path:
        blocked.append("Evidence archive is marked ready but no archive path was recorded.")
    elif os.path.basename(evidence_path) != expected_evidence_name:
        blocked.append(f"Evidence archive filename must be {expected_evidence_name}.")
    elif not os.path.exists(evidence_path):
        blocked.append("Required evidence archive is missing.")
    else:
        actual_evidence_sha = sha256(evidence_path)
        if not evidence_sha:
            blocked.append("Evidence archive SHA-256 is missing.")
        elif actual_evidence_sha != evidence_sha:
            blocked.append("Evidence archive SHA-256 does not match evidence lock summary.")
        if bundle.get("evidenceArchiveSha256") and actual_evidence_sha != bundle.get("evidenceArchiveSha256"):
            blocked.append("Evidence archive SHA-256 does not match release bundle index.")
        blocked.extend(scan_zip(evidence_path, "Evidence archive"))
elif final_status == "FINAL_DISTRIBUTION_READY":
    blocked.append("Final distribution is ready but evidence archive is not ready.")

for path in [draft_json_path, draft_md_path, bundle_path, bundle_md_path, lock_path, record_json_path, record_md_path]:
    text = read_text(path)
    if text and UNSAFE.search(text):
        blocked.append(f"Unsafe content detected in {os.path.basename(path)}.")

dry_run = final_status in {"FINAL_DISTRIBUTION_DRY_RUN_ONLY", "missing"} or readiness.get("releaseStatus") == "dryRunReady"
pending = final_status in {"FINAL_DISTRIBUTION_PENDING_QA", "FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK"}
if blocked:
    status = "GITHUB_RELEASE_ASSETS_BLOCKED"
    next_action = "Resolve blocked asset validation findings before publication."
elif final_status == "FINAL_DISTRIBUTION_READY":
    status = "GITHUB_RELEASE_ASSETS_READY"
    next_action = "Generate the publication plan, create the guarded tag, then run the publish dry-run."
elif dry_run:
    status = "GITHUB_RELEASE_ASSETS_DRY_RUN_ONLY"
    next_action = "Run credentialed finalization before public GitHub Release asset validation."
elif pending:
    status = "GITHUB_RELEASE_ASSETS_PENDING_DISTRIBUTION_READY"
    next_action = "Complete final distribution readiness before publication."
else:
    status = "GITHUB_RELEASE_ASSETS_PENDING_DISTRIBUTION_READY"
    next_action = "Complete final distribution readiness before publication."

summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.githubReleaseAssetsValidation.v1",
    "status": status,
    "version": version,
    "build": build,
    "finalDistributionStatus": final_status,
    "releaseCandidateFilename": os.path.basename(package_path),
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": package_sha,
    "expectedReleaseCandidateFilename": expected_zip_name,
    "evidenceArchiveFilename": os.path.basename(evidence_path) if evidence_path else "",
    "evidenceArchivePath": safe_path(evidence_path) if evidence_path else "",
    "evidenceArchiveSha256": evidence_sha if evidence_path else "",
    "expectedEvidenceArchiveFilename": expected_evidence_name,
    "evidenceLockStatus": lock_status,
    "blockedReasons": sorted(set(blocked)),
    "warnings": sorted(set(warnings)),
    "nextAction": next_action,
    "generatedAt": utc_now(),
}

text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("GitHub Release asset validation summary contains unsafe content.")
with open(output_json, "w", encoding="utf-8") as handle:
    handle.write(text)

print("TokenForge GitHub Release asset validation")
print(f"Status: {status}")
print(f"Release candidate: {safe_path(package_path)}")
print(f"Release candidate SHA-256: {package_sha or 'not available'}")
if evidence_path:
    print(f"Evidence archive: {safe_path(evidence_path)}")
    print(f"Evidence archive SHA-256: {evidence_sha or 'not available'}")
if blocked:
    print("Blocked reasons:")
    for reason in sorted(set(blocked)):
        print(f"- {reason}")
print(f"Next action: {next_action}")
print(f"Summary: {safe_path(output_json)}")

if status == "GITHUB_RELEASE_ASSETS_BLOCKED":
    raise SystemExit(1)
PY

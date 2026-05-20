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
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
CHECKSUMS="${EVIDENCE_DIR}/checksums.txt"
HANDOFF_MD="${EVIDENCE_DIR}/final-release-handoff.md"
RECORD_MD="${EVIDENCE_DIR}/final-release-record.md"
RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"

mkdir -p "${EVIDENCE_DIR}"

if [[ ! -f "${VERIFICATION_SUMMARY}" ]]; then
  PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${SCRIPT_DIR}/verify-credentialed-release.sh" >/dev/null 2>&1 || true
fi

/usr/bin/python3 - "${RECORD_MD}" "${RECORD_JSON}" "${EXPECTED_PRODUCT_NAME}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" "${PACKAGE_PATH}" "${READINESS_REPORT}" "${SIGNING_SUMMARY}" "${NOTARIZATION_SUMMARY}" "${CRED_RELEASE_SUMMARY}" "${VERIFICATION_SUMMARY}" "${QA_STATUS}" "${CHECKSUMS}" "${HANDOFF_MD}" "${EVIDENCE_DIR}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

(
    record_md,
    record_json,
    app_name,
    version,
    build,
    package_path,
    readiness_path,
    signing_path,
    notary_path,
    cred_path,
    verification_path,
    qa_path,
    checksums_path,
    handoff_path,
    evidence_dir,
) = sys.argv[1:]

UNSAFE = re.compile(
    r"(/Users/|/private/|~/|APPLE_ID|APPLE_TEAM_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|"
    r"DEVELOPER_ID_APPLICATION|TOKENFORGE_MACOS_SIGN_IDENTITY|BEGIN PRIVATE KEY|password\s*[:=]|"
    r"secret\s*[:=]|token\s*[:=]|Bearer\s+[A-Za-z0-9._-]+|tokenforge-approved-locations\.local\.json|"
    r"tokenforge-sync-(retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|stack trace|Traceback \(most recent call last\)|prompt:|response:|"
    r"server body|notarytool submit|xattr -d com\.apple\.quarantine|spctl\s+--master-disable)",
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


readiness = load_json(readiness_path)
signing = load_json(signing_path)
notary = load_json(notary_path)
credentialed = load_json(cred_path)
verification = load_json(verification_path)
qa = load_json(qa_path)

package_sha = sha256(package_path) or readiness.get("releaseCandidateSha256", "") or verification.get("releaseCandidateSha256", "")
release_filename = os.path.basename(package_path)
verification_status = verification.get("status", "CRED_RELEASE_VERIFY_BLOCKED")
readiness_status = readiness.get("releaseStatus", "blocked")
qa_status = verification.get("qaStatus", qa.get("qaOverallStatus", qa.get("overallStatus", "missing" if not os.path.exists(qa_path) else "inProgress")))
blocked_reasons = verification.get("blockedReasons", [])
if not isinstance(blocked_reasons, list):
    blocked_reasons = []

if verification_status == "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION":
    distribution_readiness = "ready for distribution"
elif verification_status == "CRED_RELEASE_VERIFY_DRY_RUN_ONLY" or readiness_status == "dryRunReady":
    distribution_readiness = "dry-run only"
elif verification_status == "CRED_RELEASE_VERIFIED_PENDING_QA" or qa_status in {"missing", "inProgress", "notStarted"}:
    distribution_readiness = "pending QA"
else:
    distribution_readiness = "blocked"

if notary.get("notarizationStatus") != "NOTARIZATION_SUCCEEDED" and distribution_readiness == "ready for distribution":
    distribution_readiness = "blocked"
    blocked_reasons.append("Actual notarization was not completed.")
if notary.get("spctlStatus") == "SPCTL_FAILED":
    distribution_readiness = "blocked"
if readiness.get("packageScanResult") == "failed":
    distribution_readiness = "blocked"

next_action = verification.get("nextAction") or readiness.get("nextAction") or "Resolve release gates before distribution."
if distribution_readiness == "dry-run only":
    next_action = "Run credentialed release on a configured release machine."
elif distribution_readiness == "pending QA":
    next_action = "Complete manual Gatekeeper QA and rerun verification."
elif distribution_readiness == "blocked":
    next_action = "Resolve blocked release gates and regenerate verification."
elif distribution_readiness == "ready for distribution":
    next_action = "Attach this release record and evidence summaries to the release."

privacy_assertions = readiness.get("privacyAssertions", {})
if not isinstance(privacy_assertions, dict):
    privacy_assertions = {}
known_limitations = readiness.get("knownLimitations", [])
if not isinstance(known_limitations, list):
    known_limitations = []

attachable_evidence = [
    safe_path(record_md),
    safe_path(record_json),
    safe_path(readiness_path),
    safe_path(signing_path),
    safe_path(notary_path),
    safe_path(cred_path),
    safe_path(verification_path),
    safe_path(qa_path),
    safe_path(checksums_path),
    safe_path(handoff_path),
]

record = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.finalReleaseRecord.v1",
    "releaseTitle": f"{app_name} {version} build {build}",
    "appVersion": version,
    "appBuild": build,
    "releaseCandidateFilename": release_filename,
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": package_sha,
    "signingStatus": signing.get("status", "unknown"),
    "signingIdentityType": signing.get("signingIdentityType", "unknown"),
    "hardenedRuntime": signing.get("hardenedRuntime", "unknown"),
    "notarizationStatus": notary.get("notarizationStatus", "NOTARIZATION_NOT_ATTEMPTED"),
    "staplingStatus": notary.get("staplingStatus", "notAttempted"),
    "spctlStatus": notary.get("spctlStatus", "notAttempted"),
    "packagePrivacyStatus": readiness.get("packageScanResult", "unknown"),
    "privacyScanStatus": readiness.get("privacyScanResult", "unknown"),
    "qaStatus": qa_status,
    "distributionReadiness": distribution_readiness,
    "readinessReleaseStatus": readiness_status,
    "verificationStatus": verification_status,
    "blockedReasons": sorted(set(str(reason) for reason in blocked_reasons)),
    "nextAction": next_action,
    "attachableEvidence": attachable_evidence,
    "privacyAssertions": privacy_assertions,
    "knownLimitations": known_limitations,
    "generatedAt": utc_now(),
}

json_text = json.dumps(record, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(json_text):
    raise SystemExit("Final release record JSON contains unsafe content.")
with open(record_json, "w", encoding="utf-8") as handle:
    handle.write(json_text)

def bullets(values):
    if not values:
        return "- None recorded."
    return "\n".join(f"- {value}" for value in values)

privacy_lines = [
    f"noLocalOnlyFilesInPackage: {privacy_assertions.get('noLocalOnlyFilesInPackage', False)}",
    f"noApprovedLocationPathsInEvidence: {privacy_assertions.get('noApprovedLocationPathsInEvidence', False)}",
    f"noSecretsInEvidence: {privacy_assertions.get('noSecretsInEvidence', False)}",
    f"noRawSyncStateInEvidence: {privacy_assertions.get('noRawSyncStateInEvidence', False)}",
    f"noBackgroundSyncAdded: {privacy_assertions.get('noBackgroundSyncAdded', False)}",
]

md = f"""# {record['releaseTitle']}

- Release candidate: {release_filename}
- Release candidate path: {safe_path(package_path)}
- SHA-256: {package_sha or 'not available'}
- Signing status: {record['signingStatus']}
- Signing identity type: {record['signingIdentityType']}
- Hardened runtime: {record['hardenedRuntime']}
- Notarization status: {record['notarizationStatus']}
- Stapling status: {record['staplingStatus']}
- spctl status: {record['spctlStatus']}
- Package privacy status: {record['packagePrivacyStatus']}
- QA status: {qa_status}
- Distribution readiness: {distribution_readiness}
- Exact next action: {next_action}

## Safe Sync Privacy Assertions

{bullets(privacy_lines)}

## Known Limitations

{bullets(known_limitations)}

## Blocked Reasons

{bullets(record['blockedReasons'])}

## Attachable Evidence

{bullets(attachable_evidence)}
"""
if UNSAFE.search(md):
    raise SystemExit("Final release record markdown contains unsafe content.")
with open(record_md, "w", encoding="utf-8") as handle:
    handle.write(md)

print("TokenForge final release record")
print(f"Distribution readiness: {distribution_readiness}")
print(f"QA status: {qa_status}")
print(f"Next action: {next_action}")
print(f"Markdown: {safe_path(record_md)}")
print(f"JSON: {safe_path(record_json)}")
PY

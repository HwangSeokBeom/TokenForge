#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

EXPECTED_PRODUCT_NAME="${TOKENFORGE_EXPECTED_PRODUCT_NAME:-TokenForge}"
EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
MANIFEST_PATH="${TOKENFORGE_RELEASE_FREEZE_MANIFEST:-${REPO_ROOT}/Docs/release-freeze-manifest-v${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.md}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
CRED_RELEASE_SUMMARY="${RELEASE_DIR}/credentialed-release-summary.json"
VERIFICATION_SUMMARY="${RELEASE_DIR}/credentialed-release-verification.json"
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
SIGNING_SUMMARY="${RELEASE_DIR}/signing-status-summary.json"
NOTARIZATION_SUMMARY="${RELEASE_DIR}/notarization-status-summary.json"
HANDOFF_MD="${EVIDENCE_DIR}/release-operator-handoff.md"
HANDOFF_JSON="${EVIDENCE_DIR}/release-operator-handoff.json"

mkdir -p "${EVIDENCE_DIR}"

/usr/bin/python3 - "${HANDOFF_MD}" "${HANDOFF_JSON}" "${MANIFEST_PATH}" "${READINESS_REPORT}" "${FINAL_RECORD_JSON}" "${CRED_RELEASE_SUMMARY}" "${VERIFICATION_SUMMARY}" "${QA_STATUS}" "${SIGNING_SUMMARY}" "${NOTARIZATION_SUMMARY}" "${PACKAGE_PATH}" "${EVIDENCE_DIR}" "${EXPECTED_PRODUCT_NAME}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

(
    handoff_md,
    handoff_json,
    manifest_path,
    readiness_path,
    final_record_path,
    cred_path,
    verification_path,
    qa_path,
    signing_path,
    notary_path,
    package_path,
    evidence_dir,
    app_name,
    version,
    build,
) = sys.argv[1:]

SECRET_VALUE = re.compile(
    r"(BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"(password|secret|api[_-]?key|access[_-]?token|refresh[_-]?token)[\"']?\s*[:=]\s*[\"'][^\"'\s]{8,}[\"']|AKIA[0-9A-Z]{16}|"
    r"eyJ[A-Za-z0-9_=-]{10,}\.[A-Za-z0-9_=-]{10,}\.[A-Za-z0-9_=-]{10,})",
    re.I,
)
UNSAFE_CONTENT = re.compile(
    r"(/Users/|/private/|~/|tokenforge-approved-locations\.local\.json|"
    r"tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|raw server|stack trace|Traceback \(most recent call last\)|"
    r"```[a-zA-Z]*\n(class|namespace|public|using)\s|xattr -d com\.apple\.quarantine|spctl\s+--master-disable)",
    re.I,
)
ENV_NAMES = [
    "DEVELOPER_ID_APPLICATION",
    "APPLE_ID",
    "APPLE_TEAM_ID",
    "APPLE_APP_SPECIFIC_PASSWORD",
]
COMMANDS = [
    "scripts/run-release-machine-finalization.sh --credentialed",
    "scripts/run-credentialed-release.sh --credentialed",
    "scripts/verify-credentialed-release.sh",
    "scripts/update-gatekeeper-qa-status.sh init",
    "scripts/update-gatekeeper-qa-status.sh summary",
    "scripts/update-gatekeeper-qa-status.sh validate",
    "scripts/generate-final-release-record.sh",
    "scripts/generate-release-operator-handoff.sh",
    "scripts/final-privacy-regression-audit.sh",
    "scripts/validate-release-freeze-manifest.sh",
    "scripts/lock-release-evidence.sh",
    "scripts/generate-release-readiness-report.sh",
    "scripts/release-prep-check.sh --summary-only",
    "scripts/prepare-release-tag.sh",
]


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


def qa_overall(qa):
    return qa.get("qaOverallStatus", qa.get("overallStatus", "missing" if not os.path.exists(qa_path) else "inProgress"))


readiness = load_json(readiness_path)
record = load_json(final_record_path)
credentialed = load_json(cred_path)
verification = load_json(verification_path)
qa = load_json(qa_path)
signing = load_json(signing_path)
notary = load_json(notary_path)

release_status = readiness.get("releaseStatus", "unknown")
signing_status = signing.get("status", "unknown")
signing_identity_type = signing.get("signingIdentityType", "unknown")
notary_status = notary.get("notarizationStatus", "unknown")
stapling_status = notary.get("staplingStatus", "notAttempted")
spctl_status = notary.get("spctlStatus", "notAttempted")
verification_status = verification.get("status", "notRun")
qa_status = qa_overall(qa)
package_sha = sha256(package_path) or readiness.get("releaseCandidateSha256", "") or record.get("releaseCandidateSha256", "")

if release_status == "readyForDistribution":
    decision = "ready for distribution"
elif release_status == "pendingQa" or qa_status in {"inProgress", "notStarted", "missing"}:
    decision = "pending QA"
elif str(signing_status).startswith("SIGNING_BLOCKED") or str(notary_status).startswith("NOTARIZATION_FAILED") or verification_status == "CRED_RELEASE_VERIFY_BLOCKED":
    decision = "blocked"
else:
    decision = "dry-run only"

if signing_identity_type == "adHoc" or notary_status == "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING":
    decision = "dry-run only" if decision != "blocked" else decision

expected_success = [
    "SIGNING_SUCCEEDED",
    "NOTARIZATION_SUCCEEDED",
    "STAPLE_SUCCEEDED",
    "SPCTL_SUCCEEDED",
    "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION",
    "readyForDistribution",
    "FINAL_PRIVACY_AUDIT_PASSED",
]
expected_failure = [
    "CRED_RELEASE_BLOCKED_MISSING_CREDENTIALS",
    "SIGNING_READY_BUT_IDENTITY_MISSING",
    "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING",
    "NOTARIZATION_BLOCKED_ADHOC_SIGNED",
    "CRED_RELEASE_VERIFY_DRY_RUN_ONLY",
    "CRED_RELEASE_VERIFY_BLOCKED",
    "FINAL_PRIVACY_AUDIT_FAILED",
]
privacy_assertions = [
    "No local-only sync, audit, retry, tombstone, or conflict files in the package.",
    "No approved-location paths in evidence.",
    "No raw sync state in evidence.",
    "No secrets in evidence.",
    "No background sync, retry, tombstone, or conflict processing was added.",
    "Evidence is normalized summaries and checksums only.",
]
known_limitations = readiness.get("knownLimitations", [])
if not isinstance(known_limitations, list):
    known_limitations = []
if signing_identity_type == "adHoc":
    known_limitations.append("Current local package is ad-hoc/dry-run and is not public distribution-ready.")
if notary_status != "NOTARIZATION_SUCCEEDED":
    known_limitations.append("Actual Apple notarization has not completed.")
known_limitations = sorted(set(str(item) for item in known_limitations))

handoff = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.releaseOperatorHandoff.v1",
    "appName": app_name,
    "targetPlatform": "macOS",
    "releaseType": "MVP release candidate",
    "version": version,
    "build": build,
    "releaseCandidateFilename": os.path.basename(package_path),
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": package_sha,
    "manifestPath": safe_path(manifest_path) if manifest_path.startswith("/tmp/") else "Docs/release-freeze-manifest-v0.18.0-build18.md",
    "currentReleaseState": {
        "releaseStatus": release_status,
        "signingStatus": signing_status,
        "signingIdentityType": signing_identity_type,
        "notarizationStatus": notary_status,
        "staplingStatus": stapling_status,
        "spctlStatus": spctl_status,
        "verificationStatus": verification_status,
        "qaOverallStatus": qa_status,
        "distributionDecision": decision,
    },
    "alreadyValidated": [
        "Release metadata validation is covered by release-prep-check.",
        "Package privacy scan is represented by readiness report packageScanResult.",
        "Credentialed verification summary is normalized under the release directory.",
        "Final release record is generated when verification inputs exist.",
    ],
    "notYetDone": [
        "Developer ID signing on the release machine." if signing_identity_type != "developerId" else "",
        "Apple notarization, stapling, and spctl distribution assessment." if notary_status != "NOTARIZATION_SUCCEEDED" else "",
        "Manual Gatekeeper QA completion." if qa_status != "passed" else "",
    ],
    "commandsToRunOnReleaseMachine": COMMANDS,
    "requiredEnvironmentVariables": ENV_NAMES,
    "expectedSuccessStatuses": expected_success,
    "expectedFailureStatuses": expected_failure,
    "evidenceOutputs": [
        safe_path(os.path.join(evidence_dir, "release-operator-handoff.md")),
        safe_path(os.path.join(evidence_dir, "release-operator-handoff.json")),
        safe_path(os.path.join(evidence_dir, "final-release-record.md")),
        safe_path(os.path.join(evidence_dir, "final-release-record.json")),
        safe_path(os.path.join(evidence_dir, "final-privacy-regression-audit.json")),
        safe_path(readiness_path),
        safe_path(verification_path),
        safe_path(qa_path),
    ],
    "decisionRules": {
        "dryRunOnly": "Use when signing is ad-hoc, notarization is missing, or verification reports dry-run only.",
        "pendingQa": "Use when signing, notarization, stapling, spctl, and package privacy pass but manual QA is missing or in progress.",
        "blocked": "Use when any release gate fails, summaries are invalid, or an impossible readiness claim is detected.",
        "readyForDistribution": "Use only when Developer ID signing, notarization, stapling, spctl, package privacy scan, final privacy audit, final release record, and manual QA all pass.",
        "tagAllowed": "Use only when release-machine finalization is ready for tag and evidence lock is ready.",
    },
    "privacyAssertions": privacy_assertions,
    "knownLimitations": known_limitations,
    "generatedAt": utc_now(),
}
handoff["notYetDone"] = [item for item in handoff["notYetDone"] if item]

json_text = json.dumps(handoff, indent=2, sort_keys=True) + "\n"
if SECRET_VALUE.search(json_text) or UNSAFE_CONTENT.search(json_text):
    raise SystemExit("Release operator handoff JSON contains unsafe content.")
with open(handoff_json, "w", encoding="utf-8") as handle:
    handle.write(json_text)


def bullets(values):
    if not values:
        return "- None."
    return "\n".join(f"- {value}" for value in values)


md = f"""# TokenForge Release Operator Handoff

- App: {app_name}
- Platform: macOS
- Release type: MVP release candidate
- Version/build: {version} / {build}
- Release candidate: {os.path.basename(package_path)}
- Release candidate path: {safe_path(package_path)}
- SHA-256: {package_sha or 'not available'}
- Release status: {release_status}
- Signing status: {signing_status}
- Signing identity type: {signing_identity_type}
- Notarization status: {notary_status}
- Stapling status: {stapling_status}
- spctl status: {spctl_status}
- Verification status: {verification_status}
- QA overall: {qa_status}
- Distribution decision: {decision}

## Already Validated

{bullets(handoff['alreadyValidated'])}

## Not Yet Done

{bullets(handoff['notYetDone'])}

## Required Environment Variables

{bullets(ENV_NAMES)}

## Exact Release-Machine Commands

{bullets(COMMANDS)}

## Expected Success Statuses

{bullets(expected_success)}

## Expected Failure Statuses

{bullets(expected_failure)}

## Evidence Outputs

{bullets(handoff['evidenceOutputs'])}

## Decision Rules

- Dry-run only: {handoff['decisionRules']['dryRunOnly']}
- Pending QA: {handoff['decisionRules']['pendingQa']}
- Blocked: {handoff['decisionRules']['blocked']}
- Ready for distribution: {handoff['decisionRules']['readyForDistribution']}

## Privacy Assertions

{bullets(privacy_assertions)}

## Known Limitations

{bullets(known_limitations)}
"""
if SECRET_VALUE.search(md) or UNSAFE_CONTENT.search(md):
    raise SystemExit("Release operator handoff markdown contains unsafe content.")
with open(handoff_md, "w", encoding="utf-8") as handle:
    handle.write(md)

print("TokenForge release operator handoff")
print(f"Distribution decision: {decision}")
print(f"Release status: {release_status}")
print(f"Signing status: {signing_status}")
print(f"Notarization status: {notary_status}")
print(f"QA overall: {qa_status}")
print(f"Markdown: {safe_path(handoff_md)}")
print(f"JSON: {safe_path(handoff_json)}")
PY

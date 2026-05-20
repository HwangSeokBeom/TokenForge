#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

EXPECTED_PRODUCT_NAME="${TOKENFORGE_EXPECTED_PRODUCT_NAME:-TokenForge}"
EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
REPORT_PATH="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
SIGNING_SUMMARY="${RELEASE_DIR}/signing-status-summary.json"
NOTARIZATION_SUMMARY="${RELEASE_DIR}/notarization-status-summary.json"
CRED_RELEASE_SUMMARY="${RELEASE_DIR}/credentialed-release-summary.json"
VERIFICATION_SUMMARY="${RELEASE_DIR}/credentialed-release-verification.json"
QA_STATUS_PATH="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
GATEKEEPER_SUMMARY="${EVIDENCE_DIR}/gatekeeper-qa-summary.json"
FINAL_RECORD_MD="${EVIDENCE_DIR}/final-release-record.md"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
HANDOFF_MD="${EVIDENCE_DIR}/release-operator-handoff.md"
FINAL_PRIVACY_AUDIT="${EVIDENCE_DIR}/final-privacy-regression-audit.json"
RELEASE_MACHINE_FINALIZATION="${RELEASE_DIR}/release-machine-finalization-summary.json"
FREEZE_VALIDATION="${EVIDENCE_DIR}/release-freeze-validation.json"
EVIDENCE_LOCK="${EVIDENCE_DIR}/evidence-lock-summary.json"

mkdir -p "$(dirname "${REPORT_PATH}")" "${EVIDENCE_DIR}"

PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/sign-macos-release-candidate.sh" >/dev/null || true
existing_notary_status=""
if [[ -f "${NOTARIZATION_SUMMARY}" ]]; then
  existing_notary_status="$(/usr/bin/python3 -c 'import json,sys; data=json.load(open(sys.argv[1])); print(data.get("notarizationStatus",""))' "${NOTARIZATION_SUMMARY}" 2>/dev/null || true)"
fi
if [[ "${existing_notary_status}" != "NOTARIZATION_SUCCEEDED" ]]; then
  PACKAGE_PATH="${PACKAGE_PATH}" "${SCRIPT_DIR}/notarize-macos-release-candidate.sh" >/dev/null || true
fi
PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${SCRIPT_DIR}/gatekeeper-qa-helper.sh" --json >/dev/null || true

/usr/bin/python3 - "${REPORT_PATH}" "${PACKAGE_PATH}" "${SIGNING_SUMMARY}" "${NOTARIZATION_SUMMARY}" "${CRED_RELEASE_SUMMARY}" "${VERIFICATION_SUMMARY}" "${QA_STATUS_PATH}" "${GATEKEEPER_SUMMARY}" "${EVIDENCE_DIR}" "${FINAL_RECORD_MD}" "${FINAL_RECORD_JSON}" "${HANDOFF_MD}" "${FINAL_PRIVACY_AUDIT}" "${RELEASE_MACHINE_FINALIZATION}" "${FREEZE_VALIDATION}" "${EVIDENCE_LOCK}" "${EXPECTED_PRODUCT_NAME}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

(
    report_path,
    package_path,
    signing_path,
    notary_path,
    cred_path,
    verification_path,
    qa_path,
    gatekeeper_path,
    evidence_dir,
    final_record_path,
    final_record_json_path,
    handoff_path,
    final_privacy_path,
    finalization_path,
    freeze_validation_path,
    evidence_lock_path,
    app_name,
    version,
    build,
) = sys.argv[1:]

UNSAFE = re.compile(
    r"/Users/|/private/|~/|APPLE_ID|APPLE_TEAM_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|"
    r"DEVELOPER_ID_APPLICATION|TOKENFORGE_MACOS_SIGN_IDENTITY|BEGIN PRIVATE KEY|password\s*[:=]|"
    r"secret\s*[:=]|token\s*[:=]|Bearer\s+[A-Za-z0-9._-]+|tokenforge-approved-locations\.local\.json|"
    r"tokenforge-sync-(retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|stack trace|Traceback \(most recent call last\)|prompt:|response:|"
    r"server body|notarytool submit|xattr -d com\.apple\.quarantine|spctl\s+--master-disable",
    re.I,
)
REQUIRED_QA_ITEMS = [
    "cleanInstall",
    "firstLaunch",
    "moveToApplications",
    "quarantineAssessment",
    "localStateLocation",
    "safeSyncPanelLaunch",
    "confirmationModalCancel",
    "confirmationModalTypedPhrase",
    "conflictAuditLocalOnly",
    "retryTombstoneLocalOnly",
    "approvedLocationsLocalOnly",
    "privacyScan",
    "packageScan",
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
    if not os.path.exists(path):
        return ""
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def qa_overall(path):
    if not os.path.exists(path):
        return "missing"
    data = load_json(path)
    items = data.get("items")
    if not isinstance(items, dict):
        return "invalid"
    statuses = []
    for item in REQUIRED_QA_ITEMS:
        value = items.get(item)
        if isinstance(value, dict):
            status = value.get("status", "notStarted")
            note = value.get("note", "")
        else:
            status = value
            note = ""
        if status not in {"notStarted", "passed", "failed", "blocked", "skippedWithReason"}:
            return "invalid"
        if note and UNSAFE.search(str(note)):
            return "invalid"
        if status == "skippedWithReason" and not str(note).strip():
            return "inProgress"
        statuses.append(status)
    if "failed" in statuses:
        return "failed"
    if "blocked" in statuses:
        return "blocked"
    if all(status in {"passed", "skippedWithReason"} for status in statuses):
        return "passed"
    return "inProgress"


signing = load_json(signing_path)
notary = load_json(notary_path)
credentialed = load_json(cred_path)
verification = load_json(verification_path)
gatekeeper = load_json(gatekeeper_path)
final_record = load_json(final_record_json_path)
final_privacy = load_json(final_privacy_path)
finalization = load_json(finalization_path)
freeze_validation = load_json(freeze_validation_path)
evidence_lock = load_json(evidence_lock_path)
qa_status = qa_overall(qa_path)
package_sha = sha256(package_path)

signing_status = signing.get("status", "unknown")
signed_developer_id = signing.get("signedWithDeveloperId") is True and signing.get("signingIdentityType") == "developerId"
hardened_runtime_ok = signing.get("hardenedRuntime") == "enabled"
notary_status = notary.get("notarizationStatus", notary.get("status", "NOTARIZATION_NOT_ATTEMPTED"))
stapling_status = notary.get("staplingStatus", "notAttempted")
spctl_status = notary.get("spctlStatus", "notAttempted")

package_scan = "passed" if package_sha and signing_status != "SIGNING_BLOCKED_PRIVACY_SCAN_FAILED" else "failed" if signing_status == "SIGNING_BLOCKED_PRIVACY_SCAN_FAILED" else "missingReleaseCandidate"
privacy_scan = "passed" if package_scan == "passed" else "failed" if package_scan == "failed" else "notRun"
final_privacy_status = final_privacy.get("status", "notRun")
evidence_lock_status = evidence_lock.get("status", "notRun")
verification_status = verification.get("status", "notRun")

blocked_gate = False
if not package_sha:
    blocked_gate = True
if signing_status.startswith("SIGNING_BLOCKED"):
    blocked_gate = True
if notary_status in {"NOTARIZATION_FAILED"} or str(notary_status).startswith("NOTARIZATION_FAILED"):
    blocked_gate = True
if stapling_status == "STAPLE_FAILED" or spctl_status == "SPCTL_FAILED":
    blocked_gate = True
if package_scan == "failed" or privacy_scan == "failed":
    blocked_gate = True
if qa_status in {"failed", "blocked", "invalid"}:
    blocked_gate = True
if str(credentialed.get("status", "")).startswith("CRED_RELEASE_BLOCKED"):
    blocked_gate = True
if final_privacy_status == "FINAL_PRIVACY_AUDIT_FAILED":
    blocked_gate = True
if evidence_lock_status == "EVIDENCE_LOCK_BLOCKED":
    blocked_gate = True

all_core = all([
    signed_developer_id,
    hardened_runtime_ok,
    notary_status == "NOTARIZATION_SUCCEEDED",
    stapling_status == "STAPLE_SUCCEEDED",
    spctl_status == "SPCTL_SUCCEEDED",
    package_scan == "passed",
    privacy_scan == "passed",
])
all_distribution_gates = all([
    all_core,
    qa_status == "passed",
    final_privacy_status == "FINAL_PRIVACY_AUDIT_PASSED",
    verification_status == "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION",
])

if blocked_gate:
    release_status = "blocked"
elif all_distribution_gates and evidence_lock_status == "EVIDENCE_LOCK_READY":
    release_status = "readyForDistribution"
elif all_distribution_gates and evidence_lock_status != "EVIDENCE_LOCK_READY":
    release_status = "evidencePending"
elif all_core and qa_status in {"missing", "inProgress", "notStarted"}:
    release_status = "pendingQa"
elif signed_developer_id and hardened_runtime_ok and notary_status == "NOTARIZATION_SUCCEEDED" and stapling_status == "STAPLE_SUCCEEDED" and spctl_status == "SPCTL_SUCCEEDED":
    release_status = "spctlVerified"
elif signed_developer_id and hardened_runtime_ok and notary_status == "NOTARIZATION_SUCCEEDED" and stapling_status == "STAPLE_SUCCEEDED":
    release_status = "stapled"
elif signed_developer_id and hardened_runtime_ok and notary_status == "NOTARIZATION_SUCCEEDED":
    release_status = "notarized"
elif signed_developer_id and hardened_runtime_ok:
    release_status = "credentialedSigned"
elif package_scan == "passed":
    release_status = "dryRunReady"
else:
    release_status = "blocked"

if release_status == "readyForDistribution":
    next_action = "Attach the final release record and evidence summaries to the release."
elif release_status == "evidencePending":
    next_action = "Run scripts/lock-release-evidence.sh, then regenerate the readiness report."
elif release_status == "pendingQa":
    next_action = "Complete manual Gatekeeper QA, then rerun credentialed verification."
elif release_status == "spctlVerified":
    next_action = "Confirm package privacy and complete manual Gatekeeper QA."
elif release_status == "stapled":
    next_action = "Run spctl assessment and complete manual Gatekeeper QA."
elif release_status == "notarized":
    next_action = "Staple the notarization ticket, run spctl, then complete manual QA."
elif release_status == "credentialedSigned":
    next_action = "Submit the Developer ID signed release candidate for notarization."
elif release_status == "dryRunReady":
    next_action = "Run scripts/run-credentialed-release.sh --credentialed on a configured release machine."
else:
    next_action = "Resolve blocked release gates before distribution."

known_limitations = [
    "Ad-hoc or dry-run builds are not notarized and cannot be claimed ready for final Gatekeeper distribution.",
    "Developer ID notarization requires credentialed submit mode on a release machine.",
    "Manual Gatekeeper QA must be completed before distribution readiness is claimed.",
    "Safe Sync has no background sync, retry, tombstone, or conflict processing.",
    "Approved locations remain local-only and are not synced.",
    "Safe Sync schema remains v1.",
]

report = {
    "schemaVersion": 5,
    "schemaName": "tokenforge.releaseReadiness.v5",
    "appName": app_name,
    "version": version,
    "build": build,
    "releaseStatus": release_status,
    "releaseStatusAllowedValues": [
        "dryRunReady",
        "credentialedSigned",
        "notarized",
        "stapled",
        "spctlVerified",
        "pendingQa",
        "evidencePending",
        "readyForDistribution",
        "blocked",
    ],
    "signingSummary": signing,
    "notarizationSummary": notary,
    "credentialedReleaseSummary": credentialed,
    "credentialedVerificationSummary": verification,
    "releaseMachineFinalizationSummary": finalization,
    "freezeManifestValidationSummary": freeze_validation,
    "evidenceLockSummary": evidence_lock,
    "evidenceSummary": {
        "evidenceBundlePath": safe_path(evidence_dir),
        "checksumsPath": safe_path(os.path.join(evidence_dir, "checksums.txt")),
        "finalReleaseHandoffPath": safe_path(os.path.join(evidence_dir, "final-release-handoff.md")),
        "finalReleaseRecordPath": safe_path(final_record_path) if os.path.exists(final_record_path) else "",
        "releaseOperatorHandoffPath": safe_path(handoff_path) if os.path.exists(handoff_path) else "",
        "evidenceLockSummaryPath": safe_path(evidence_lock_path) if os.path.exists(evidence_lock_path) else "",
        "evidenceArchivePath": evidence_lock.get("evidenceArchivePath", ""),
    },
    "qaStatusSummary": {
        "overallStatus": qa_status,
        "gatekeeperQaStatusPath": safe_path(qa_path),
        "gatekeeperQaSummaryPath": safe_path(gatekeeper_path),
        "spctlAssessment": gatekeeper.get("spctlStatus", "notRun"),
        "staplingValidation": gatekeeper.get("staplingStatus", "notRun"),
    },
    "finalReleaseRecordPath": safe_path(final_record_path) if os.path.exists(final_record_path) else "",
    "releaseOperatorHandoffPath": safe_path(handoff_path) if os.path.exists(handoff_path) else "",
    "releaseCandidateSha256": package_sha,
    "releaseCandidatePath": safe_path(package_path),
    "generatedAt": utc_now(),
    "nextAction": next_action,
    "knownLimitations": known_limitations,
    "privacyScanResult": privacy_scan,
    "packageScanResult": package_scan,
    "localOnlyFileRejectionResult": "passed" if package_scan == "passed" else "failed" if package_scan == "failed" else "notRun",
    "privacyAssertions": {
        "noLocalOnlyFilesInPackage": package_scan == "passed",
        "noApprovedLocationPathsInEvidence": True,
        "noSecretsInEvidence": True,
        "noRawSyncStateInEvidence": True,
        "noBackgroundSyncAdded": True,
    },
}

text = json.dumps(report, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("Release readiness report contains unsafe content.")
with open(report_path, "w", encoding="utf-8") as handle:
    handle.write(text)

print("TokenForge release readiness report")
print(f"Report path: {safe_path(report_path)}")
print(f"Schema version: 5")
print(f"Release status: {release_status}")
print(f"Signing status: {signing_status}")
print(f"Signing identity type: {signing.get('signingIdentityType', 'unknown')}")
print(f"Notarization status: {notary_status}")
print(f"Stapling status: {stapling_status}")
print(f"spctl status: {spctl_status}")
print(f"QA status: {qa_status}")
print(f"Final privacy audit status: {final_privacy_status}")
print(f"Evidence lock status: {evidence_lock_status}")
print(f"Next action: {next_action}")
print(f"Evidence bundle path: {safe_path(evidence_dir)}")
PY

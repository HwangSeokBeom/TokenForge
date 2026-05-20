#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
SIGNING_SUMMARY="${RELEASE_DIR}/signing-status-summary.json"
NOTARIZATION_SUMMARY="${RELEASE_DIR}/notarization-status-summary.json"
CRED_RELEASE_SUMMARY="${RELEASE_DIR}/credentialed-release-summary.json"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
VERIFICATION_SUMMARY="${RELEASE_DIR}/credentialed-release-verification.json"

mkdir -p "${RELEASE_DIR}" "${EVIDENCE_DIR}"

/usr/bin/python3 - "${VERIFICATION_SUMMARY}" "${PACKAGE_PATH}" "${SIGNING_SUMMARY}" "${NOTARIZATION_SUMMARY}" "${CRED_RELEASE_SUMMARY}" "${READINESS_REPORT}" "${QA_STATUS}" "${EVIDENCE_DIR}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

summary_path, package_path, signing_path, notary_path, cred_path, readiness_path, qa_path, evidence_dir = sys.argv[1:]

UNSAFE = re.compile(
    r"(/Users/|/private/|~/|APPLE_ID|APPLE_TEAM_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|"
    r"DEVELOPER_ID_APPLICATION|TOKENFORGE_MACOS_SIGN_IDENTITY|BEGIN PRIVATE KEY|password\s*[:=]|"
    r"secret\s*[:=]|token\s*[:=]|Bearer\s+[A-Za-z0-9._-]+|tokenforge-approved-locations\.local\.json|"
    r"tokenforge-sync-(retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|stack trace|Traceback \(most recent call last\)|prompt:|response:|"
    r"server body|notarytool submit|xattr -d com\.apple\.quarantine|spctl\s+--master-disable)",
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
ALLOWED_QA = {"notStarted", "passed", "failed", "blocked", "skippedWithReason"}


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def safe_path(value):
    if value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/"):
        return value
    return "controlled-path"


def load_json(path):
    if not os.path.exists(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data if isinstance(data, dict) else None
    except Exception:
        return None


def sha256(path):
    if not os.path.isfile(path):
        return ""
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def nested(data, key, default=None):
    return data.get(key, default) if isinstance(data, dict) else default


def qa_summary(path):
    if not os.path.exists(path):
        return "missing", True, []
    data = load_json(path)
    if not data:
        return "invalid", False, ["QA status file is invalid."]
    items = data.get("items")
    if not isinstance(items, dict):
        return "invalid", False, ["QA status file is missing required checklist items."]
    statuses = []
    for item in REQUIRED_QA_ITEMS:
        value = items.get(item)
        if isinstance(value, dict):
            status = value.get("status")
            note = value.get("note", "")
        else:
            status = value
            note = ""
        if status not in ALLOWED_QA:
            return "invalid", False, [f"QA item {item} has an invalid status."]
        if note and UNSAFE.search(str(note)):
            return "invalid", False, ["QA status file contains unsafe notes."]
        if status == "skippedWithReason" and not str(note).strip():
            return "invalid", False, [f"QA item {item} requires a safe note for skippedWithReason."]
        statuses.append(status)
    if "failed" in statuses:
        computed = "failed"
    elif "blocked" in statuses:
        computed = "blocked"
    elif all(status in {"passed", "skippedWithReason"} for status in statuses):
        computed = "passed"
    else:
        computed = "inProgress"
    recorded = data.get("qaOverallStatus", data.get("overallStatus", computed))
    if recorded not in {computed, "notStarted"}:
        return "invalid", False, ["QA status aggregate does not match checklist items."]
    return computed, True, []


signing = load_json(signing_path)
notary = load_json(notary_path)
credentialed = load_json(cred_path)
readiness = load_json(readiness_path)
blocked = []

package_sha = sha256(package_path)
if not package_sha:
    blocked.append("Release candidate zip is missing.")

for label, data in [
    ("signing summary", signing),
    ("notarization summary", notary),
    ("readiness report", readiness),
]:
    if data is None:
        blocked.append(f"Required {label} is missing or invalid.")

expected_sha_values = []
for data in [signing, notary, credentialed, readiness]:
    value = nested(data, "releaseCandidateSha256", "")
    if value:
        expected_sha_values.append(value)
if package_sha and expected_sha_values and any(value != package_sha for value in expected_sha_values):
    blocked.append("Release candidate SHA-256 does not match the latest summaries.")

signing_ok = bool(signing and signing.get("signedWithDeveloperId") is True and signing.get("signingIdentityType") == "developerId" and signing.get("hardenedRuntime") == "enabled")
notarization_ok = bool(notary and notary.get("notarizationStatus") == "NOTARIZATION_SUCCEEDED")
stapling_ok = bool(notary and notary.get("staplingStatus") == "STAPLE_SUCCEEDED")
spctl_ok = bool(notary and notary.get("spctlStatus") == "SPCTL_SUCCEEDED")
privacy_assertions = readiness.get("privacyAssertions", {}) if isinstance(readiness, dict) else {}
package_privacy_ok = bool(
    readiness
    and readiness.get("packageScanResult") == "passed"
    and readiness.get("privacyScanResult") == "passed"
    and privacy_assertions.get("noLocalOnlyFilesInPackage") is True
)
handoff = os.path.join(evidence_dir, "final-release-handoff.md")
evidence_ok = os.path.isdir(evidence_dir) and os.path.isfile(handoff)
qa_status, qa_valid, qa_reasons = qa_summary(qa_path)
blocked.extend(qa_reasons)

dry_run_only = False
if signing and not signing_ok:
    signing_status = signing.get("status", "")
    identity_type = signing.get("signingIdentityType", "")
    notary_status = notary.get("notarizationStatus", "") if notary else ""
    dry_run_only = (
        identity_type == "adHoc"
        or signing_status in {"SIGNING_DRY_RUN_READY", "SIGNING_READY_BUT_IDENTITY_MISSING"}
        or notary_status in {"NOTARIZATION_READY_BUT_CREDENTIALS_MISSING", "NOTARIZATION_BLOCKED_ADHOC_SIGNED", "NOTARIZATION_NOT_ATTEMPTED"}
    )

if signing and signing.get("status", "").startswith("SIGNING_BLOCKED"):
    blocked.append("Signing summary is blocked.")
if notary and notary.get("notarizationStatus", "").startswith("NOTARIZATION_FAILED"):
    blocked.append("Notarization summary is failed.")
if notary and notary.get("staplingStatus") == "STAPLE_FAILED":
    blocked.append("Stapling failed.")
if notary and notary.get("spctlStatus") == "SPCTL_FAILED":
    blocked.append("spctl assessment failed.")
if readiness and readiness.get("packageScanResult") == "failed":
    blocked.append("Package privacy scan failed.")
if readiness and readiness.get("privacyScanResult") == "failed":
    blocked.append("Privacy scan failed.")
if not evidence_ok:
    blocked.append("Evidence bundle or final handoff is missing.")
if qa_status in {"failed", "blocked", "invalid"}:
    blocked.append("Gatekeeper QA is not passing.")

all_release_gates = all([signing_ok, notarization_ok, stapling_ok, spctl_ok, package_privacy_ok, evidence_ok])
if blocked:
    status = "CRED_RELEASE_VERIFY_BLOCKED"
    next_action = "Resolve blocked release verification gates before distribution."
elif dry_run_only:
    status = "CRED_RELEASE_VERIFY_DRY_RUN_ONLY"
    next_action = "Run credentialed release on a configured release machine."
elif all_release_gates and qa_status == "passed":
    status = "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION"
    next_action = "Attach final release record and evidence summaries to the release."
elif all_release_gates and qa_status in {"missing", "inProgress", "notStarted"}:
    status = "CRED_RELEASE_VERIFIED_PENDING_QA"
    next_action = "Complete manual Gatekeeper QA with scripts/update-gatekeeper-qa-status.sh."
else:
    status = "CRED_RELEASE_VERIFY_BLOCKED"
    next_action = "Resolve incomplete signing, notarization, stapling, spctl, privacy, or evidence gates."
    if not signing_ok:
        blocked.append("Developer ID signing with hardened runtime is incomplete.")
    if not notarization_ok:
        blocked.append("Notarization success is incomplete.")
    if not stapling_ok:
        blocked.append("Stapling success is incomplete.")
    if not spctl_ok:
        blocked.append("spctl success is incomplete.")
    if not package_privacy_ok:
        blocked.append("Package privacy scan is incomplete.")

summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.credentialedReleaseVerification.v1",
    "status": status,
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": package_sha,
    "signingOk": signing_ok,
    "notarizationOk": notarization_ok,
    "staplingOk": stapling_ok,
    "spctlOk": spctl_ok,
    "packagePrivacyOk": package_privacy_ok,
    "evidenceOk": evidence_ok,
    "qaStatus": qa_status,
    "blockedReasons": sorted(set(blocked)),
    "nextAction": next_action,
    "generatedAt": utc_now(),
}

text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("Credentialed release verification summary contains unsafe content.")
with open(summary_path, "w", encoding="utf-8") as handle:
    handle.write(text)

print("TokenForge credentialed release verification")
print(f"Status: {status}")
print(f"Release candidate: {safe_path(package_path)}")
print(f"SHA-256: {package_sha or 'not available'}")
print(f"Signing OK: {str(signing_ok).lower()}")
print(f"Notarization OK: {str(notarization_ok).lower()}")
print(f"Stapling OK: {str(stapling_ok).lower()}")
print(f"spctl OK: {str(spctl_ok).lower()}")
print(f"Package privacy OK: {str(package_privacy_ok).lower()}")
print(f"Evidence OK: {str(evidence_ok).lower()}")
print(f"QA status: {qa_status}")
if summary["blockedReasons"]:
    print("Blocked reasons:")
    for reason in summary["blockedReasons"]:
        print(f"- {reason}")
print(f"Next action: {next_action}")
print(f"Verification summary: {safe_path(summary_path)}")

if status == "CRED_RELEASE_VERIFY_BLOCKED":
    raise SystemExit(1)
PY

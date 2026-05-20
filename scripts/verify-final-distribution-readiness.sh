#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
CRED_RELEASE_SUMMARY="${RELEASE_DIR}/credentialed-release-summary.json"
VERIFICATION_SUMMARY="${RELEASE_DIR}/credentialed-release-verification.json"
FINALIZATION_SUMMARY="${RELEASE_DIR}/release-machine-finalization-summary.json"
EVIDENCE_LOCK="${EVIDENCE_DIR}/evidence-lock-summary.json"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
FINAL_PRIVACY_AUDIT="${EVIDENCE_DIR}/final-privacy-regression-audit.json"
FREEZE_VALIDATION="${EVIDENCE_DIR}/release-freeze-validation.json"
OUTPUT_JSON="${RELEASE_DIR}/final-distribution-readiness.json"

mkdir -p "${RELEASE_DIR}" "${EVIDENCE_DIR}"

/usr/bin/python3 - "${OUTPUT_JSON}" "${PACKAGE_PATH}" "${READINESS_REPORT}" "${CRED_RELEASE_SUMMARY}" "${VERIFICATION_SUMMARY}" "${FINALIZATION_SUMMARY}" "${EVIDENCE_LOCK}" "${FINAL_RECORD_JSON}" "${QA_STATUS}" "${FINAL_PRIVACY_AUDIT}" "${FREEZE_VALIDATION}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

(
    output_path,
    package_path,
    readiness_path,
    credentialed_path,
    verification_path,
    finalization_path,
    evidence_lock_path,
    final_record_path,
    qa_path,
    privacy_audit_path,
    freeze_path,
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
FINAL_FREEZE_OK = {
    "FREEZE_MANIFEST_VALID_FINAL_MATCH",
    "FREEZE_MANIFEST_ACCEPTED_FINAL_STATE",
    "FREEZE_MANIFEST_VALID_FINAL_STATE",
}
SAFE_FREEZE_OK = FINAL_FREEZE_OK | {"FREEZE_MANIFEST_VALID_DRY_RUN_DRIFT_DOCUMENTED"}


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


def first(*values):
    for value in values:
        if value not in (None, ""):
            return value
    return ""


readiness = load_json(readiness_path)
credentialed = load_json(credentialed_path)
verification = load_json(verification_path)
finalization = load_json(finalization_path)
lock = load_json(evidence_lock_path)
record = load_json(final_record_path)
qa = load_json(qa_path)
audit = load_json(privacy_audit_path)
freeze = load_json(freeze_path)

signing = readiness.get("signingSummary", {}) if isinstance(readiness.get("signingSummary"), dict) else {}
notary = readiness.get("notarizationSummary", {}) if isinstance(readiness.get("notarizationSummary"), dict) else {}
privacy_assertions = readiness.get("privacyAssertions", {}) if isinstance(readiness.get("privacyAssertions"), dict) else {}

package_sha = sha256(package_path)
release_status = readiness.get("releaseStatus", "missing")
record_readiness = record.get("distributionReadiness", "missing")
qa_status = first(
    qa.get("qaOverallStatus"),
    qa.get("overallStatus"),
    verification.get("qaStatus"),
    readiness.get("qaStatusSummary", {}).get("overallStatus") if isinstance(readiness.get("qaStatusSummary"), dict) else "",
    "missing",
)
lock_status = lock.get("status", "missing")
freeze_status = freeze.get("status", "missing")
audit_status = audit.get("status", "missing")
verification_status = verification.get("status", "missing")
finalization_status = finalization.get("status", "missing")

signing_ok = (
    signing.get("signedWithDeveloperId") is True
    and signing.get("signingIdentityType") == "developerId"
    and signing.get("hardenedRuntime") == "enabled"
)
notarization_ok = notary.get("notarizationStatus") == "NOTARIZATION_SUCCEEDED"
stapling_ok = notary.get("staplingStatus") == "STAPLE_SUCCEEDED"
spctl_ok = notary.get("spctlStatus") == "SPCTL_SUCCEEDED"
qa_ok = qa_status == "passed"
privacy_ok = all([
    readiness.get("packageScanResult") == "passed",
    readiness.get("privacyScanResult") == "passed",
    privacy_assertions.get("noLocalOnlyFilesInPackage") is True,
    audit_status == "FINAL_PRIVACY_AUDIT_PASSED",
])
evidence_lock_ok = lock_status == "EVIDENCE_LOCK_READY"
freeze_ok = freeze_status in SAFE_FREEZE_OK
final_freeze_ok = freeze_status in FINAL_FREEZE_OK
readiness_ok = release_status == "readyForDistribution"
record_ready_values = {"ready for distribution", "readyForDistribution", "FINAL_RELEASE_READY_FOR_DISTRIBUTION"}
final_record_ok = record_readiness in record_ready_values or record.get("readyForDistribution") is True

blocked = []
if not package_sha:
    blocked.append("Release candidate zip is missing.")

sha_sources = {
    "readiness report": readiness.get("releaseCandidateSha256", ""),
    "final release record": record.get("releaseCandidateSha256", ""),
    "evidence lock": lock.get("releaseCandidateSha256", ""),
}
for label, value in sha_sources.items():
    if value and package_sha and value != package_sha:
        blocked.append(f"SHA-256 mismatch between current release candidate and {label}.")
if package_sha and any(not value for value in sha_sources.values()):
    blocked.append("Required SHA-256 value is missing from final summaries.")

if release_status == "readyForDistribution" and not all([signing_ok, notarization_ok, stapling_ok, spctl_ok, qa_ok, privacy_ok, evidence_lock_ok, final_freeze_ok, final_record_ok]):
    blocked.append("Readiness report overclaims readyForDistribution before every final release gate passed.")
if final_record_ok and not all([signing_ok, notarization_ok, stapling_ok, spctl_ok, qa_ok, privacy_ok, readiness_ok]):
    blocked.append("Final release record overclaims distribution readiness before every final release gate passed.")
if freeze_status.startswith("FREEZE_MANIFEST_BLOCKED"):
    blocked.append("Release freeze manifest validation is blocked.")
if str(signing.get("status", "")).startswith("SIGNING_BLOCKED"):
    blocked.append("Signing gate is blocked.")
if str(notary.get("notarizationStatus", "")).startswith("NOTARIZATION_FAILED"):
    blocked.append("Notarization gate failed.")
if notary.get("staplingStatus") == "STAPLE_FAILED":
    blocked.append("Stapling gate failed.")
if notary.get("spctlStatus") == "SPCTL_FAILED":
    blocked.append("spctl gate failed.")
if audit_status == "FINAL_PRIVACY_AUDIT_FAILED":
    blocked.append("Final privacy regression audit failed.")
if verification_status == "CRED_RELEASE_VERIFY_BLOCKED":
    blocked.append("Credentialed release verification is blocked.")

all_final_gates = all([
    signing_ok,
    notarization_ok,
    stapling_ok,
    spctl_ok,
    qa_ok,
    privacy_ok,
    evidence_lock_ok,
    final_freeze_ok,
    readiness_ok,
    final_record_ok,
    verification_status == "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION",
    finalization_status in {"RELEASE_MACHINE_READY_FOR_TAG", "missing", "notRun"},
])
dry_run = (
    signing.get("signingIdentityType") == "adHoc"
    or signing.get("signedWithDeveloperId") is not True
    or release_status == "dryRunReady"
    or verification_status == "CRED_RELEASE_VERIFY_DRY_RUN_ONLY"
    or notary.get("notarizationStatus") in {"NOTARIZATION_READY_BUT_CREDENTIALS_MISSING", "NOTARIZATION_BLOCKED_ADHOC_SIGNED", "NOTARIZATION_NOT_ATTEMPTED"}
)

tag_allowed = all_final_gates and not blocked
if blocked:
    status = "FINAL_DISTRIBUTION_BLOCKED"
    next_action = "Resolve blocked final distribution gates before tag creation or GitHub Release publication."
elif tag_allowed:
    status = "FINAL_DISTRIBUTION_READY"
    next_action = "Generate the GitHub Release draft, bundle index, status dashboard, then create the guarded release tag."
elif dry_run:
    status = "FINAL_DISTRIBUTION_DRY_RUN_ONLY"
    next_action = "Run release-machine finalization with Developer ID signing and Apple notarization credentials."
elif not qa_ok:
    status = "FINAL_DISTRIBUTION_PENDING_QA"
    next_action = "Complete Gatekeeper QA, rerun credentialed verification, final record, audit, evidence lock, and readiness report."
elif not evidence_lock_ok:
    status = "FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK"
    next_action = "Lock final evidence, regenerate readiness report, then rerun final distribution verification."
else:
    status = "FINAL_DISTRIBUTION_BLOCKED"
    blocked.append("One or more final distribution gates are incomplete.")
    next_action = "Complete all final release gates before publication."

summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.finalDistributionReadiness.v1",
    "status": status,
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": package_sha,
    "signingOk": signing_ok,
    "notarizationOk": notarization_ok,
    "staplingOk": stapling_ok,
    "spctlOk": spctl_ok,
    "qaOk": qa_ok,
    "privacyOk": privacy_ok,
    "evidenceLockOk": evidence_lock_ok,
    "freezeOk": freeze_ok,
    "readinessOk": readiness_ok,
    "finalRecordOk": final_record_ok,
    "tagAllowed": tag_allowed,
    "blockedReasons": sorted(set(blocked)),
    "nextAction": next_action,
    "generatedAt": utc_now(),
}

text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("Final distribution readiness summary contains unsafe content.")
with open(output_path, "w", encoding="utf-8") as handle:
    handle.write(text)

print("TokenForge final distribution readiness")
print(f"Status: {status}")
print(f"Release candidate: {safe_path(package_path)}")
print(f"SHA-256: {package_sha or 'not available'}")
print(f"Signing OK: {str(signing_ok).lower()}")
print(f"Notarization OK: {str(notarization_ok).lower()}")
print(f"Stapling OK: {str(stapling_ok).lower()}")
print(f"spctl OK: {str(spctl_ok).lower()}")
print(f"QA OK: {str(qa_ok).lower()}")
print(f"Privacy OK: {str(privacy_ok).lower()}")
print(f"Evidence lock OK: {str(evidence_lock_ok).lower()}")
print(f"Freeze OK: {str(freeze_ok).lower()}")
print(f"Readiness OK: {str(readiness_ok).lower()}")
print(f"Final record OK: {str(final_record_ok).lower()}")
print(f"Tag allowed: {str(tag_allowed).lower()}")
if blocked:
    print("Blocked reasons:")
    for reason in sorted(set(blocked)):
        print(f"- {reason}")
print(f"Next action: {next_action}")
print(f"Summary: {safe_path(output_path)}")

if status == "FINAL_DISTRIBUTION_BLOCKED":
    raise SystemExit(1)
PY

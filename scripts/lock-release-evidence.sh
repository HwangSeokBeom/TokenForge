#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
VERIFICATION_SUMMARY="${RELEASE_DIR}/credentialed-release-verification.json"
FINALIZATION_SUMMARY="${RELEASE_DIR}/release-machine-finalization-summary.json"
FINAL_RECORD_MD="${EVIDENCE_DIR}/final-release-record.md"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
HANDOFF_MD="${EVIDENCE_DIR}/release-operator-handoff.md"
HANDOFF_JSON="${EVIDENCE_DIR}/release-operator-handoff.json"
FINAL_PRIVACY_AUDIT="${EVIDENCE_DIR}/final-privacy-regression-audit.json"
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
LOCK_SUMMARY="${EVIDENCE_DIR}/evidence-lock-summary.json"
ARCHIVE_PATH="${RELEASE_DIR}/TokenForge-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}-release-evidence.zip"

mkdir -p "${EVIDENCE_DIR}"

/usr/bin/python3 - "${EVIDENCE_DIR}" "${PACKAGE_PATH}" "${READINESS_REPORT}" "${VERIFICATION_SUMMARY}" "${FINALIZATION_SUMMARY}" "${FINAL_RECORD_MD}" "${FINAL_RECORD_JSON}" "${HANDOFF_MD}" "${HANDOFF_JSON}" "${FINAL_PRIVACY_AUDIT}" "${QA_STATUS}" "${LOCK_SUMMARY}" "${ARCHIVE_PATH}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys
import zipfile

(
    evidence_dir,
    package_path,
    readiness_path,
    verification_path,
    finalization_path,
    final_record_md,
    final_record_json,
    handoff_md,
    handoff_json,
    final_privacy_path,
    qa_path,
    lock_summary_path,
    archive_path,
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
SAFE_BASENAMES = {
    "release-readiness-report.json",
    "credentialed-release-verification.json",
    "release-machine-finalization-summary.json",
    "release-freeze-validation.json",
    "final-release-record.md",
    "final-release-record.json",
    "release-operator-handoff.md",
    "release-operator-handoff.json",
    "final-privacy-regression-audit.json",
    "gatekeeper-qa-status.json",
    "gatekeeper-qa-summary.json",
    "release-evidence.json",
    "release-evidence.md",
    "checksums.txt",
}
FORBIDDEN_BASENAME = re.compile(
    r"tokenforge-(approved-locations|sync-state|sync-conflicts|sync-retry|sync-retry-queue|sync-tombstones|sync-conflict-audit|sync-local-state)\.local\.json|"
    r"\.log$|\.env$|\.p8$|\.p12$|private[_-]?key|credential-values?",
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


def qa_overall(qa):
    return qa.get("qaOverallStatus", qa.get("overallStatus", "missing" if not os.path.exists(qa_path) else "inProgress"))


def file_is_safe(path):
    if FORBIDDEN_BASENAME.search(os.path.basename(path)):
        return False, "forbidden filename"
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as handle:
            text = handle.read()
    except OSError:
        return False, "unreadable file"
    if UNSAFE.search(text):
        return False, "unsafe content"
    return True, ""


readiness = load_json(readiness_path)
verification = load_json(verification_path)
finalization = load_json(finalization_path)
audit = load_json(final_privacy_path)
qa = load_json(qa_path)
notary = readiness.get("notarizationSummary", {}) if isinstance(readiness.get("notarizationSummary"), dict) else {}
signing = readiness.get("signingSummary", {}) if isinstance(readiness.get("signingSummary"), dict) else {}
privacy = readiness.get("privacyAssertions", {}) if isinstance(readiness.get("privacyAssertions"), dict) else {}

release_status = readiness.get("releaseStatus", "unknown")
qa_status = qa_overall(qa) or readiness.get("qaStatusSummary", {}).get("overallStatus", "missing")
verification_status = verification.get("status", "notRun")
audit_status = audit.get("status", "notRun")
finalization_status = finalization.get("status", "notRun")
release_gates_pass = all([
    signing.get("signedWithDeveloperId") is True,
    signing.get("signingIdentityType") == "developerId",
    signing.get("hardenedRuntime") == "enabled",
    notary.get("notarizationStatus") == "NOTARIZATION_SUCCEEDED",
    notary.get("staplingStatus") == "STAPLE_SUCCEEDED",
    notary.get("spctlStatus") == "SPCTL_SUCCEEDED",
    readiness.get("packageScanResult") == "passed",
    readiness.get("privacyScanResult") == "passed",
    privacy.get("noLocalOnlyFilesInPackage") is True,
    qa_status == "passed",
    verification_status == "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION",
    audit_status == "FINAL_PRIVACY_AUDIT_PASSED",
    finalization_status in {"RELEASE_MACHINE_READY_FOR_TAG", "notRun"},
])

blocked_reasons = []
if qa_status in {"missing", "inProgress", "notStarted"}:
    blocked_reasons.append("Gatekeeper QA is not passed.")
if release_status == "blocked":
    blocked_reasons.append("Readiness report is blocked.")
if audit_status == "FINAL_PRIVACY_AUDIT_FAILED":
    blocked_reasons.append("Final privacy audit failed.")
if verification_status == "CRED_RELEASE_VERIFY_BLOCKED":
    blocked_reasons.append("Credentialed release verification is blocked.")

safe_files = []
for candidate in [
    readiness_path,
    verification_path,
    finalization_path,
    os.path.join(evidence_dir, "release-freeze-validation.json"),
    final_record_md,
    final_record_json,
    handoff_md,
    handoff_json,
    final_privacy_path,
    qa_path,
    os.path.join(evidence_dir, "gatekeeper-qa-summary.json"),
    os.path.join(evidence_dir, "release-evidence.json"),
    os.path.join(evidence_dir, "release-evidence.md"),
    os.path.join(evidence_dir, "checksums.txt"),
]:
    if not os.path.isfile(candidate):
        continue
    if os.path.basename(candidate) not in SAFE_BASENAMES:
        blocked_reasons.append(f"Unsafe evidence filename excluded: {os.path.basename(candidate)}")
        continue
    ok, reason = file_is_safe(candidate)
    if not ok:
        blocked_reasons.append(f"Unsafe evidence content detected in {os.path.basename(candidate)}: {reason}")
        continue
    safe_files.append(candidate)

archive_sha = ""
included = []
if release_gates_pass and not any(reason.startswith("Unsafe evidence content") for reason in blocked_reasons):
    if os.path.exists(archive_path):
        os.remove(archive_path)
    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(set(safe_files)):
            arcname = os.path.basename(path)
            archive.write(path, arcname)
            included.append(arcname)
    archive_sha = sha256(archive_path)
    status = "EVIDENCE_LOCK_READY"
    next_action = "Generate readiness report again, then prepare the guarded release tag."
elif qa_status in {"missing", "inProgress", "notStarted"} and release_status not in {"dryRunReady", "blocked"}:
    status = "EVIDENCE_LOCK_PENDING_QA"
    next_action = "Complete Gatekeeper QA before locking final evidence."
elif release_status == "dryRunReady" or signing.get("signingIdentityType") == "adHoc":
    status = "EVIDENCE_LOCK_DRY_RUN_ONLY"
    next_action = "Run credentialed release finalization on the release machine."
else:
    status = "EVIDENCE_LOCK_BLOCKED"
    next_action = "Resolve blocked or unsafe evidence before distribution."

summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.evidenceLock.v1",
    "status": status,
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": sha256(package_path) or readiness.get("releaseCandidateSha256", ""),
    "readinessReleaseStatus": release_status,
    "verificationStatus": verification_status,
    "releaseMachineFinalizationStatus": finalization_status,
    "finalPrivacyAuditStatus": audit_status,
    "qaOverallStatus": qa_status,
    "evidenceArchivePath": safe_path(archive_path) if status == "EVIDENCE_LOCK_READY" else "",
    "evidenceArchiveSha256": archive_sha,
    "includedEvidenceFiles": included,
    "blockedReasons": sorted(set(blocked_reasons)),
    "nextAction": next_action,
    "generatedAt": utc_now(),
}

text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("Evidence lock summary contains unsafe content.")
with open(lock_summary_path, "w", encoding="utf-8") as handle:
    handle.write(text)

print("TokenForge release evidence lock")
print(f"Status: {status}")
if status == "EVIDENCE_LOCK_READY":
    print(f"Evidence archive: {safe_path(archive_path)}")
    print(f"Evidence archive SHA-256: {archive_sha}")
print(f"Summary: {safe_path(lock_summary_path)}")
if blocked_reasons:
    print("Blocked reasons:")
    for reason in sorted(set(blocked_reasons)):
        print(f"- {reason}")

if status == "EVIDENCE_LOCK_BLOCKED":
    raise SystemExit(1)
PY

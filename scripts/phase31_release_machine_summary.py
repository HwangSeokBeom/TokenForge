#!/usr/bin/env python3
import datetime
import hashlib
import json
import os
import re
import sys

summary_path, release_dir, evidence_dir, package_path = sys.argv[1:]

UNSAFE = re.compile(
    r"/Users/|/private/|~/|BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"(password|secret|api[_-]?key|access[_-]?token|refresh[_-]?token)[\"']?\s*[:=]\s*[\"'][^\"'\s]{8,}[\"']|"
    r"tokenforge-approved-locations\.local\.json|tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|prompt:|response:|Traceback \(most recent call last\)|stack trace",
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


readiness = load_json(os.path.join(release_dir, "release-readiness-report.json"))
signing = load_json(os.path.join(release_dir, "signing-status-summary.json"))
notary = load_json(os.path.join(release_dir, "notarization-status-summary.json"))
verification = load_json(os.path.join(release_dir, "credentialed-release-verification.json"))
audit = load_json(os.path.join(evidence_dir, "final-privacy-regression-audit.json"))
qa = load_json(os.path.join(evidence_dir, "gatekeeper-qa-status.json"))
lock = load_json(os.path.join(evidence_dir, "evidence-lock-summary.json"))

qa_status = qa.get("qaOverallStatus", qa.get("overallStatus", readiness.get("qaStatusSummary", {}).get("overallStatus", "missing")))
blocked = []
external_block = os.environ.get("TOKENFORGE_RELEASE_MACHINE_BLOCKED_REASON", "")
if external_block:
    blocked.append(external_block)

signing_status = signing.get("status", "unknown")
notary_status = notary.get("notarizationStatus", "unknown")
stapling_status = notary.get("staplingStatus", "notAttempted")
spctl_status = notary.get("spctlStatus", "notAttempted")
readiness_status = readiness.get("releaseStatus", "unknown")
verification_status = verification.get("status", "notRun")
audit_status = audit.get("status", "notRun")
lock_status = lock.get("status", "notRun")

core_ready = all([
    signing.get("signedWithDeveloperId") is True,
    signing.get("signingIdentityType") == "developerId",
    signing.get("hardenedRuntime") == "enabled",
    notary_status == "NOTARIZATION_SUCCEEDED",
    stapling_status == "STAPLE_SUCCEEDED",
    spctl_status == "SPCTL_SUCCEEDED",
    readiness.get("packageScanResult") == "passed",
    readiness.get("privacyScanResult") == "passed",
    audit_status == "FINAL_PRIVACY_AUDIT_PASSED",
    verification_status in {"CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION", "CRED_RELEASE_VERIFIED_PENDING_QA"},
])

if signing_status.startswith("SIGNING_BLOCKED"):
    blocked.append("Signing gate is blocked.")
if str(notary_status).startswith("NOTARIZATION_FAILED"):
    blocked.append("Notarization gate failed.")
if stapling_status == "STAPLE_FAILED":
    blocked.append("Stapling gate failed.")
if spctl_status == "SPCTL_FAILED":
    blocked.append("spctl gate failed.")
if audit_status == "FINAL_PRIVACY_AUDIT_FAILED":
    blocked.append("Final privacy audit failed.")
if readiness_status == "blocked":
    blocked.append("Readiness report is blocked.")
if verification_status == "CRED_RELEASE_VERIFY_BLOCKED":
    blocked.append("Credentialed release verification is blocked.")

if blocked:
    status = "RELEASE_MACHINE_BLOCKED"
    next_action = "Resolve blocked release gates before tagging or distribution."
elif core_ready and qa_status != "passed":
    status = "RELEASE_MACHINE_PENDING_QA"
    next_action = "Complete Gatekeeper QA, regenerate evidence, then rerun finalization."
elif core_ready and qa_status == "passed" and readiness_status in {"readyForDistribution", "evidencePending"}:
    status = "RELEASE_MACHINE_READY_FOR_TAG"
    next_action = "Lock evidence if needed, regenerate readiness report, then run prepare-release-tag with guarded flags."
elif signing.get("signingIdentityType") == "adHoc" or readiness_status == "dryRunReady":
    status = "RELEASE_MACHINE_BLOCKED"
    blocked.append("Current state is dry-run/ad-hoc only.")
    next_action = "Run this script with --credentialed on a release machine with Developer ID credentials."
else:
    status = "RELEASE_MACHINE_BLOCKED"
    blocked.append("Required credentialed release gates are incomplete.")
    next_action = "Complete Developer ID signing, notarization, stapling, spctl, privacy audit, and QA."

summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.releaseMachineFinalization.v1",
    "status": status,
    "credentialedRunAttempted": os.environ.get("TOKENFORGE_RELEASE_MACHINE_CRED_ATTEMPTED", "false") == "true",
    "signingStatus": signing_status,
    "notarizationStatus": notary_status,
    "staplingStatus": stapling_status,
    "spctlStatus": spctl_status,
    "qaOverallStatus": qa_status,
    "finalPrivacyAuditStatus": audit_status,
    "readinessReleaseStatus": readiness_status,
    "verificationStatus": verification_status,
    "evidenceLockStatus": lock_status,
    "releaseCandidatePath": safe_path(package_path),
    "releaseCandidateSha256": sha256(package_path) or readiness.get("releaseCandidateSha256", ""),
    "finalReleaseRecordPath": safe_path(os.path.join(evidence_dir, "final-release-record.md")),
    "releaseOperatorHandoffPath": safe_path(os.path.join(evidence_dir, "release-operator-handoff.md")),
    "blockedReasons": sorted(set(blocked)),
    "nextAction": next_action,
    "generatedAt": utc_now(),
}

text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("Release-machine finalization summary contains unsafe content.")
with open(summary_path, "w", encoding="utf-8") as handle:
    handle.write(text)

print("TokenForge release-machine finalization")
print(f"Status: {status}")
print(f"Credentialed run attempted: {str(summary['credentialedRunAttempted']).lower()}")
print(f"Signing status: {signing_status}")
print(f"Notarization status: {notary_status}")
print(f"Stapling status: {stapling_status}")
print(f"spctl status: {spctl_status}")
print(f"QA status: {qa_status}")
print(f"Readiness releaseStatus: {readiness_status}")
print(f"Verification status: {verification_status}")
print(f"Release candidate: {safe_path(package_path)}")
print(f"SHA-256: {summary['releaseCandidateSha256'] or 'not available'}")
if summary["blockedReasons"]:
    print("Blocked reasons:")
    for reason in summary["blockedReasons"]:
        print(f"- {reason}")
print(f"Next action: {next_action}")
print(f"Summary: {safe_path(summary_path)}")

if status == "RELEASE_MACHINE_BLOCKED":
    raise SystemExit(1)

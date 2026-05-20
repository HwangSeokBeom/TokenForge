#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"
AUDIT_JSON="${EVIDENCE_DIR}/final-privacy-regression-audit.json"

mkdir -p "${EVIDENCE_DIR}"

/usr/bin/python3 - "${REPO_ROOT}" "${PACKAGE_PATH}" "${EVIDENCE_DIR}" "${READINESS_REPORT}" "${FINAL_RECORD_JSON}" "${QA_STATUS}" "${AUDIT_JSON}" <<'PY'
import datetime
import json
import os
import re
import subprocess
import sys
import tempfile
import zipfile

repo_root, package_path, evidence_dir, readiness_path, final_record_path, qa_path, audit_path = sys.argv[1:]

PACKAGE_FORBIDDEN_NAME = re.compile(
    r"(tokenforge-sync-state\.local\.json|tokenforge-sync-conflicts\.local\.json|"
    r"tokenforge-sync-retry\.local\.json|tokenforge-sync-retry-queue\.local\.json|"
    r"tokenforge-sync-tombstones\.local\.json|tokenforge-sync-conflict-audit\.local\.json|"
    r"tokenforge-sync-local-state\.local\.json|tokenforge-approved-locations\.local\.json|"
    r"approved-location|\.DS_Store$|\.env$|\.p8$|\.p12$|private[_-]?key|"
    r"\.log$|/Logs?/|/Library/|/Temp/|/Cache/|/obj/|/Build/|credentials?|notary)",
    re.I,
)
SECRET_VALUE = re.compile(
    r"(BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"(password|secret|api[_-]?key|access[_-]?token|refresh[_-]?token)[\"']?\s*[:=]\s*[\"'][^\"'\s]{8,}[\"']|AKIA[0-9A-Z]{16}|"
    r"eyJ[A-Za-z0-9_=-]{10,}\.[A-Za-z0-9_=-]{10,}\.[A-Za-z0-9_=-]{10,})",
    re.I,
)
EVIDENCE_FORBIDDEN = re.compile(
    r"(/Users/|/private/|~/|tokenforge-approved-locations\.local\.json|"
    r"tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|raw server|Traceback \(most recent call last\)|"
    r"Exception:|stack trace|prompt:|response:|source snippet|```[a-zA-Z]*\n(class|namespace|public|using)\s|"
    r"\bcommit\s+[0-9a-f]{7,40}\b)",
    re.I,
)
DOC_SECRET = SECRET_VALUE
SCRIPT_ECHO_SECRET = re.compile(
    r"echo\s+.*\$\{?(DEVELOPER_ID_APPLICATION|APPLE_ID|APPLE_TEAM_ID|APPLE_APP_SPECIFIC_PASSWORD)\}?",
    re.I,
)
ALLOWED_QA = {"notStarted", "passed", "failed", "blocked", "skippedWithReason"}


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


def scan_text_file(path, pattern, label, failures):
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as handle:
            text = handle.read()
    except OSError:
        return
    if pattern.search(text):
        failures.append(f"{label}: unsafe content in {os.path.basename(path)}")


failures = []
package_entries_checked = 0
package_text_files_checked = 0

if not os.path.isfile(package_path):
    failures.append("release package is missing")
else:
    if not zipfile.is_zipfile(package_path):
        failures.append("release package is not a valid zip")
    else:
        with zipfile.ZipFile(package_path) as archive:
            names = archive.namelist()
            package_entries_checked = len(names)
            for name in names:
                if PACKAGE_FORBIDDEN_NAME.search(name):
                    failures.append(f"package contains forbidden entry {name}")
            with tempfile.TemporaryDirectory(prefix="tokenforge-final-privacy-") as temp_dir:
                archive.extractall(temp_dir)
                for root, _, files in os.walk(temp_dir):
                    for filename in files:
                        path = os.path.join(root, filename)
                        rel = os.path.relpath(path, temp_dir)
                        if PACKAGE_FORBIDDEN_NAME.search(rel):
                            failures.append(f"package contains forbidden extracted file {rel}")
                        if filename.endswith((".json", ".txt", ".plist", ".xml", ".config", ".log", ".env", ".p8", ".p12")):
                            package_text_files_checked += 1
                            scan_text_file(path, SECRET_VALUE, "package", failures)
                            scan_text_file(path, re.compile(r"tokenforge-approved-locations\.local\.json|raw[ _-]?sync[ _-]?payload", re.I), "package", failures)

if os.path.isdir(evidence_dir):
    for root, _, files in os.walk(evidence_dir):
        for filename in files:
            path = os.path.join(root, filename)
            if filename.endswith((".json", ".md", ".txt")):
                scan_text_file(path, SECRET_VALUE, "evidence", failures)
                scan_text_file(path, EVIDENCE_FORBIDDEN, "evidence", failures)
else:
    failures.append("evidence bundle is missing")

for folder in ["Docs"]:
    target = os.path.join(repo_root, folder)
    if not os.path.isdir(target):
        continue
    for root, _, files in os.walk(target):
        for filename in files:
            if not filename.endswith((".md", ".sh", ".txt")):
                continue
            path = os.path.join(root, filename)
            scan_text_file(path, DOC_SECRET, f"{folder} secret scan", failures)

scripts_dir = os.path.join(repo_root, "scripts")
for root, _, files in os.walk(scripts_dir):
    for filename in files:
        if filename.endswith(".sh"):
            scan_text_file(os.path.join(root, filename), SCRIPT_ECHO_SECRET, "script credential echo scan", failures)

readiness = load_json(readiness_path)
signing = readiness.get("signingSummary", {}) if isinstance(readiness.get("signingSummary"), dict) else {}
if readiness.get("releaseStatus") == "readyForDistribution":
    dry_or_adhoc = signing.get("signingIdentityType") == "adHoc" or signing.get("signedWithDeveloperId") is not True
    notary = readiness.get("notarizationSummary", {}) if isinstance(readiness.get("notarizationSummary"), dict) else {}
    incomplete = notary.get("notarizationStatus") != "NOTARIZATION_SUCCEEDED" or notary.get("staplingStatus") != "STAPLE_SUCCEEDED" or notary.get("spctlStatus") != "SPCTL_SUCCEEDED"
    if dry_or_adhoc or incomplete:
        failures.append("readiness report overclaims readyForDistribution in dry-run/ad-hoc or incomplete state")

record = load_json(final_record_path)
if record.get("distributionReadiness") == "ready for distribution":
    if readiness.get("releaseStatus") != "readyForDistribution":
        failures.append("final release record overclaims ready for distribution without readiness support")
    if record.get("signingIdentityType") == "adHoc" or record.get("notarizationStatus") != "NOTARIZATION_SUCCEEDED":
        failures.append("final release record overclaims ready for distribution in dry-run/ad-hoc or incomplete state")

qa = load_json(qa_path)
items = qa.get("items", {}) if isinstance(qa.get("items"), dict) else {}
for item, value in items.items():
    status = value.get("status") if isinstance(value, dict) else value
    note = value.get("note", "") if isinstance(value, dict) else ""
    if status not in ALLOWED_QA:
        failures.append(f"QA item {item} has invalid status")
    if status == "skippedWithReason" and not str(note).strip():
        failures.append(f"QA item {item} skippedWithReason is missing a note")
    if note and (SECRET_VALUE.search(str(note)) or EVIDENCE_FORBIDDEN.search(str(note))):
        failures.append(f"QA item {item} note contains unsafe content")

status = "FINAL_PRIVACY_AUDIT_FAILED" if failures else "FINAL_PRIVACY_AUDIT_PASSED"
summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.finalPrivacyRegressionAudit.v1",
    "status": status,
    "releaseCandidatePath": safe_path(package_path),
    "evidenceBundlePath": safe_path(evidence_dir),
    "readinessReportPath": safe_path(readiness_path),
    "finalReleaseRecordPath": safe_path(final_record_path),
    "packageEntriesChecked": package_entries_checked,
    "packageTextFilesChecked": package_text_files_checked,
    "failureCount": len(failures),
    "failures": sorted(set(failures)),
    "assertions": {
        "packageHasNoLocalOnlySyncState": not any("package contains" in failure and "tokenforge-sync" in failure for failure in failures),
        "packageHasNoApprovedLocationFiles": not any("approved" in failure.lower() for failure in failures),
        "evidenceHasNoSecrets": not any(failure.startswith("evidence") for failure in failures),
        "readinessDoesNotOverclaim": not any("readiness report overclaims" in failure for failure in failures),
        "finalRecordDoesNotOverclaim": not any("final release record overclaims" in failure for failure in failures),
        "scriptsDoNotEchoCredentialValues": not any("credential echo" in failure for failure in failures),
    },
    "generatedAt": utc_now(),
}
with open(audit_path, "w", encoding="utf-8") as handle:
    json.dump(summary, handle, indent=2, sort_keys=True)
    handle.write("\n")

print("TokenForge final privacy regression audit")
print(f"Status: {status}")
print(f"Release candidate: {safe_path(package_path)}")
print(f"Evidence bundle: {safe_path(evidence_dir)}")
print(f"Audit JSON: {safe_path(audit_path)}")
if failures:
    print("Failures:")
    for failure in sorted(set(failures)):
        print(f"- {failure}")
    raise SystemExit(1)
PY

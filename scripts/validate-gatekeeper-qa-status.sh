#!/usr/bin/env bash
set -euo pipefail

RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
QA_STATUS="${TOKENFORGE_GATEKEEPER_QA_STATUS:-${EVIDENCE_DIR}/gatekeeper-qa-status.json}"

safe_path() {
  local value="$1"
  if [[ "${value}" == /tmp/tokenforge-release/* || "${value}" == /tmp/tokenforge-macos-build/* ]]; then
    echo "${value}"
  else
    echo "controlled-path"
  fi
}

if [[ ! -f "${QA_STATUS}" ]]; then
  echo "QA_STATUS_MISSING"
  echo "QA_STATUS_PATH: $(safe_path "${QA_STATUS}")"
  exit 0
fi

validation_status="$(/usr/bin/python3 - "${QA_STATUS}" <<'PY'
import json
import re
import sys

path = sys.argv[1]
allowed = {"notStarted", "passed", "failed", "blocked", "skippedWithReason"}
required = [
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
unsafe_note = re.compile(
    r"(/Users/|/private/|~\/|\{[^}\n]*\"[^}\n]*\}|Bearer\s+[A-Za-z0-9._-]+|token\s*[:=]|"
    r"password\s*[:=]|secret\s*[:=]|\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b|"
    r"APPLE_ID|APPLE_TEAM_ID|APPLE_APP_SPECIFIC_PASSWORD|APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|"
    r"stack trace|Exception:|Traceback \(most recent call last\)|prompt:|response:|"
    r"\x60\x60\x60|class\s+\w+|public\s+(class|void|string)|tokenforge-approved-locations\.local\.json|"
    r"tokenforge-sync-(retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|raw server|/Volumes/[^ \n]+|/[A-Za-z0-9._ -]+/[A-Za-z0-9._ -]+)",
    re.I,
)

try:
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
except Exception:
    print("QA_STATUS_INVALID")
    raise SystemExit

overall = data.get("overallStatus")
qa_overall = data.get("qaOverallStatus", overall)
items = data.get("items")
if qa_overall not in {"inProgress", "passed", "failed", "blocked", "notStarted"} or not isinstance(items, dict):
    print("QA_STATUS_INVALID")
    raise SystemExit

statuses = []
for key in required:
    value = items.get(key)
    note = ""
    if isinstance(value, dict):
        status = value.get("status")
        note = value.get("note", "")
    else:
        status = value
    if status not in allowed:
        print("QA_STATUS_INVALID")
        raise SystemExit
    if note is not None and not isinstance(note, str):
        print("QA_STATUS_INVALID")
        raise SystemExit
    if note and unsafe_note.search(note):
        print("QA_STATUS_UNSAFE_NOTES")
        raise SystemExit
    if status == "skippedWithReason" and not str(note).strip():
        print("QA_STATUS_INVALID")
        raise SystemExit
    statuses.append(status)

computed = "inProgress"
if "failed" in statuses:
    computed = "failed"
elif "blocked" in statuses:
    computed = "blocked"
elif all(status in {"passed", "skippedWithReason"} for status in statuses):
    computed = "passed"
if qa_overall != computed and not (qa_overall == "notStarted" and computed == "inProgress"):
    print("QA_STATUS_INVALID")
    raise SystemExit

notes = data.get("notes", "")
if notes is not None and not isinstance(notes, str):
    print("QA_STATUS_INVALID")
    raise SystemExit
if notes and unsafe_note.search(notes):
    print("QA_STATUS_UNSAFE_NOTES")
    raise SystemExit

print("QA_STATUS_VALID")
PY
)"

echo "${validation_status}"
echo "QA_STATUS_PATH: $(safe_path "${QA_STATUS}")"

case "${validation_status}" in
  QA_STATUS_VALID|QA_STATUS_MISSING)
    exit 0
    ;;
  *)
    exit 1
    ;;
esac

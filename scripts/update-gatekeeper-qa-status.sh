#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
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

usage() {
  echo "Usage: scripts/update-gatekeeper-qa-status.sh init|summary|validate|set <item> <status>|note <item> <safe note>"
  exit 1
}

command="${1:-}"
case "${command}" in
  init|summary|validate)
    if [[ "$#" -ne 1 ]]; then usage; fi
    ;;
  set)
    if [[ "$#" -ne 3 ]]; then usage; fi
    ;;
  note)
    if [[ "$#" -ne 3 ]]; then usage; fi
    ;;
  --help|-h|"")
    usage
    ;;
  *)
    echo "Unknown command: ${command}" >&2
    usage
    ;;
esac

mkdir -p "${EVIDENCE_DIR}"

if [[ "${command}" == "validate" ]]; then
  TOKENFORGE_GATEKEEPER_QA_STATUS="${QA_STATUS}" "${SCRIPT_DIR}/validate-gatekeeper-qa-status.sh"
  exit $?
fi

/usr/bin/python3 - "${command}" "${QA_STATUS}" "${2:-}" "${3:-}" <<'PY'
import datetime
import json
import os
import re
import sys

command, path, item_arg, value_arg = sys.argv[1:5]

REQUIRED_ITEMS = [
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
ALLOWED_STATUSES = {"notStarted", "passed", "failed", "blocked", "skippedWithReason"}
UNSAFE_NOTE = re.compile(
    r"(/Users/|/private/|~/|\{[^}\n]*\"[^}\n]*\}|Bearer\s+[A-Za-z0-9._-]+|"
    r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b|"
    r"token\s*[:=]|password\s*[:=]|secret\s*[:=]|APPLE_ID|APPLE_TEAM_ID|APPLE_APP_SPECIFIC_PASSWORD|"
    r"APP_SPECIFIC_PASSWORD|DEVELOPER_ID_APPLICATION|BEGIN PRIVATE KEY|stack trace|Exception:|"
    r"Traceback \(most recent call last\)|prompt:|response:|server body|raw server|```|"
    r"class\s+\w+|public\s+(class|void|string)|namespace\s+\w+|tokenforge-approved-locations\.local\.json|"
    r"tokenforge-sync-(retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|/Volumes/[^ \n]+|/[A-Za-z0-9._ -]+/[A-Za-z0-9._ -]+)",
    re.I,
)


def safe_path(value):
    if value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/"):
        return value
    return "controlled-path"


def blank_item():
    return {"status": "notStarted", "note": ""}


def normalize(data):
    if not isinstance(data, dict):
        data = {}
    raw_items = data.get("items")
    normalized = {}
    if isinstance(raw_items, dict):
        for item in REQUIRED_ITEMS:
            value = raw_items.get(item, "notStarted")
            if isinstance(value, dict):
                normalized[item] = {
                    "status": str(value.get("status", "notStarted")),
                    "note": str(value.get("note", "")),
                }
            else:
                normalized[item] = {"status": str(value), "note": ""}
    else:
        normalized = {item: blank_item() for item in REQUIRED_ITEMS}
    return {
        "schemaVersion": 2,
        "schemaName": "tokenforge.gatekeeperQaStatus.v2",
        "qaOverallStatus": aggregate(normalized),
        "overallStatus": aggregate(normalized),
        "items": normalized,
        "generatedAt": data.get("generatedAt", ""),
        "updatedAt": utc_now(),
    }


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def load():
    if not os.path.exists(path):
        return normalize({})
    with open(path, "r", encoding="utf-8") as handle:
        return normalize(json.load(handle))


def validate_note(note):
    if note and UNSAFE_NOTE.search(note):
        raise SystemExit("QA note contains unsafe content.")


def aggregate(items):
    statuses = []
    for item in REQUIRED_ITEMS:
        value = items.get(item, blank_item())
        status = value.get("status", "notStarted") if isinstance(value, dict) else str(value)
        note = value.get("note", "") if isinstance(value, dict) else ""
        if status not in ALLOWED_STATUSES:
            return "inProgress"
        if status == "skippedWithReason" and not note.strip():
            return "inProgress"
        statuses.append(status)
    if "failed" in statuses:
        return "failed"
    if "blocked" in statuses:
        return "blocked"
    if all(status in {"passed", "skippedWithReason"} for status in statuses):
        return "passed"
    return "inProgress"


def write(data):
    data["qaOverallStatus"] = aggregate(data["items"])
    data["overallStatus"] = data["qaOverallStatus"]
    if not data.get("generatedAt"):
        data["generatedAt"] = utc_now()
    data["updatedAt"] = utc_now()
    for item in REQUIRED_ITEMS:
        entry = data["items"].get(item)
        if not isinstance(entry, dict) or entry.get("status") not in ALLOWED_STATUSES:
            raise SystemExit("QA status contains an invalid checklist item.")
        validate_note(str(entry.get("note", "")))
    text = json.dumps(data, indent=2, sort_keys=True) + "\n"
    if UNSAFE_NOTE.search(text):
        raise SystemExit("QA status file contains unsafe content.")
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(text)


if command == "init":
    data = {
        "schemaVersion": 2,
        "schemaName": "tokenforge.gatekeeperQaStatus.v2",
        "qaOverallStatus": "inProgress",
        "overallStatus": "inProgress",
        "items": {item: blank_item() for item in REQUIRED_ITEMS},
        "generatedAt": utc_now(),
        "updatedAt": utc_now(),
    }
    write(data)
    print("QA_STATUS_INITIALIZED")
    print(f"QA_STATUS_PATH: {safe_path(path)}")
elif command == "set":
    if item_arg not in REQUIRED_ITEMS:
        raise SystemExit("Unknown QA checklist item.")
    if value_arg not in ALLOWED_STATUSES:
        raise SystemExit("Unknown QA status.")
    data = load()
    data["items"][item_arg]["status"] = value_arg
    write(data)
    print("QA_STATUS_UPDATED")
    print(f"ITEM: {item_arg}")
    print(f"STATUS: {value_arg}")
    print(f"QA_OVERALL_STATUS: {data['qaOverallStatus']}")
elif command == "note":
    if item_arg not in REQUIRED_ITEMS:
        raise SystemExit("Unknown QA checklist item.")
    validate_note(value_arg)
    data = load()
    data["items"][item_arg]["note"] = value_arg
    write(data)
    print("QA_NOTE_UPDATED")
    print(f"ITEM: {item_arg}")
    print(f"QA_OVERALL_STATUS: {data['qaOverallStatus']}")
elif command == "summary":
    data = load()
    print("TokenForge Gatekeeper QA status")
    print(f"Status: {data['qaOverallStatus']}")
    for item in REQUIRED_ITEMS:
        entry = data["items"][item]
        suffix = " (note)" if entry.get("note", "").strip() else ""
        print(f"- {item}: {entry['status']}{suffix}")
    print(f"QA status path: {safe_path(path)}")
PY

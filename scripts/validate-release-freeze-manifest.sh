#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
MANIFEST_PATH="${TOKENFORGE_RELEASE_FREEZE_MANIFEST:-${REPO_ROOT}/Docs/release-freeze-manifest-v${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.md}"
READINESS_REPORT="${TOKENFORGE_RELEASE_READINESS_REPORT:-${RELEASE_DIR}/release-readiness-report.json}"
FINAL_RECORD_JSON="${EVIDENCE_DIR}/final-release-record.json"
VALIDATION_JSON="${EVIDENCE_DIR}/release-freeze-validation.json"
UPDATE=false

for arg in "$@"; do
  case "${arg}" in
    --update)
      UPDATE=true
      ;;
    --help|-h)
      echo "Usage: scripts/validate-release-freeze-manifest.sh [--update]"
      echo "Validates tracked release-freeze metadata against current safe release summaries."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

mkdir -p "${EVIDENCE_DIR}"

/usr/bin/python3 - "${MANIFEST_PATH}" "${READINESS_REPORT}" "${FINAL_RECORD_JSON}" "${PACKAGE_PATH}" "${VALIDATION_JSON}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" "${UPDATE}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

manifest_path, readiness_path, final_record_path, package_path, validation_path, expected_version, expected_build, update = sys.argv[1:]
update = update == "true"
expected_filename = f"TokenForge-macOS-{expected_version}-build{expected_build}.zip"

UNSAFE = re.compile(
    r"BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY|Bearer\s+[A-Za-z0-9._-]{20,}|"
    r"(password|secret|api[_-]?key|access[_-]?token|refresh[_-]?token)[\"']?\s*[:=]\s*[\"'][^\"'\s]{8,}[\"']|"
    r"tokenforge-approved-locations\.local\.json|tokenforge-sync-(state|retry|retry-queue|tombstones|conflicts|conflict-audit|local-state)\.local\.json|"
    r"raw[ _-]?sync[ _-]?payload|server body|prompt:|response:|Traceback \(most recent call last\)",
    re.I,
)
SHA_RE = re.compile(r"\b[a-f0-9]{64}\b", re.I)


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def safe_path(value):
    if value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/"):
        return value
    if "Docs/release-freeze-manifest" in value:
        return os.path.relpath(value, os.getcwd())
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


manifest_text = ""
failures = []
if os.path.exists(manifest_path):
    with open(manifest_path, "r", encoding="utf-8") as handle:
        manifest_text = handle.read()
else:
    failures.append("release freeze manifest is missing")

readiness = load_json(readiness_path)
final_record = load_json(final_record_path)
release_status = readiness.get("releaseStatus", "unknown")
readiness_sha = readiness.get("releaseCandidateSha256", "")
final_sha = final_record.get("releaseCandidateSha256", "")
current_sha = sha256(package_path)
manifest_shas = sorted(set(match.group(0).lower() for match in SHA_RE.finditer(manifest_text)))

version_match = f"Version: {expected_version}" in manifest_text or f"version: {expected_version}" in manifest_text
build_match = f"Build: {expected_build}" in manifest_text or f"build: {expected_build}" in manifest_text
filename_match = expected_filename in manifest_text
dry_run_drift_documented = (
    "dry-run SHA drift policy" in manifest_text
    or "regenerated dry-run SHA-256" in manifest_text
    or "dry-run rebuilds may legitimately regenerate zip SHA" in manifest_text
)
distribution_claimed = "ready for public distribution" in manifest_text or "readyForDistribution" in manifest_text
dry_run_limitation_documented = "dry-run only, not ready for public distribution" in manifest_text
final_sha_documented = bool(final_sha and final_sha.lower() in manifest_text.lower())
current_sha_documented = bool(current_sha and current_sha.lower() in manifest_text.lower())
readiness_sha_documented = bool(readiness_sha and readiness_sha.lower() in manifest_text.lower())
sha_matches_known_documented = bool(current_sha and current_sha in manifest_shas) or bool(readiness_sha and readiness_sha in manifest_shas) or final_sha_documented

status = "FREEZE_MANIFEST_VALID_DRY_RUN_DRIFT_DOCUMENTED"
blocked_reason = ""
if failures:
    status = "FREEZE_MANIFEST_BLOCKED_FILENAME_MISMATCH"
    blocked_reason = "; ".join(failures)
elif not version_match or readiness.get("version", expected_version) != expected_version:
    status = "FREEZE_MANIFEST_BLOCKED_VERSION_MISMATCH"
    blocked_reason = "Manifest or readiness report version does not match expected release metadata."
elif not build_match or str(readiness.get("build", expected_build)) != str(expected_build):
    status = "FREEZE_MANIFEST_BLOCKED_BUILD_MISMATCH"
    blocked_reason = "Manifest or readiness report build does not match expected release metadata."
elif not filename_match or os.path.basename(package_path) != expected_filename:
    status = "FREEZE_MANIFEST_BLOCKED_FILENAME_MISMATCH"
    blocked_reason = "Manifest or release package filename does not match expected release metadata."
elif release_status == "readyForDistribution" and (not final_sha or not final_sha_documented or final_sha != current_sha):
    status = "FREEZE_MANIFEST_BLOCKED_READY_OVERCLAIM"
    blocked_reason = "readyForDistribution requires a final verified SHA recorded in final release evidence."
elif distribution_claimed and not dry_run_limitation_documented and release_status != "readyForDistribution":
    status = "FREEZE_MANIFEST_BLOCKED_READY_OVERCLAIM"
    blocked_reason = "Manifest claims distribution readiness while release summaries are not final-ready."
elif current_sha and manifest_shas and not sha_matches_known_documented and not dry_run_drift_documented:
    status = "FREEZE_MANIFEST_BLOCKED_UNDOCUMENTED_SHA_DRIFT"
    blocked_reason = "Current package SHA differs from the tracked manifest and dry-run drift is not documented."
elif release_status == "readyForDistribution" and final_sha and final_sha_documented and final_sha == current_sha:
    status = "FREEZE_MANIFEST_VALID_FINAL_MATCH"
else:
    status = "FREEZE_MANIFEST_VALID_DRY_RUN_DRIFT_DOCUMENTED"

if update:
    if not os.path.exists(manifest_path):
        raise SystemExit("Cannot update missing release freeze manifest.")
    addition = []
    if current_sha and current_sha.lower() not in manifest_text.lower():
        addition.append(f"- Current regenerated dry-run SHA-256: {current_sha}")
    if final_sha and final_sha.lower() not in manifest_text.lower() and release_status == "readyForDistribution":
        addition.append(f"- Credentialed final SHA-256: {final_sha}")
    if addition:
        updated = manifest_text.rstrip() + "\n\n## Phase 31 SHA Tracking Update\n\n" + "\n".join(addition) + "\n"
        if UNSAFE.search(updated):
            raise SystemExit("Refusing to update manifest with unsafe content.")
        with open(manifest_path, "w", encoding="utf-8") as handle:
            handle.write(updated)

summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.releaseFreezeValidation.v1",
    "status": status,
    "manifestPath": "Docs/release-freeze-manifest-v0.18.0-build18.md",
    "expectedVersion": expected_version,
    "expectedBuild": expected_build,
    "expectedFilename": expected_filename,
    "readinessReleaseStatus": release_status,
    "documentedFrozenSha256": manifest_shas[0] if manifest_shas else "",
    "documentedSha256Values": manifest_shas,
    "currentRegeneratedDryRunSha256": current_sha,
    "readinessSha256": readiness_sha,
    "credentialedFinalSha256": final_sha if release_status == "readyForDistribution" else "",
    "dryRunShaDriftDocumented": dry_run_drift_documented,
    "blockedReason": blocked_reason,
    "generatedAt": utc_now(),
}

text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("Release freeze validation summary contains unsafe content.")
with open(validation_path, "w", encoding="utf-8") as handle:
    handle.write(text)

print("TokenForge release freeze manifest validation")
print(f"Status: {status}")
print(f"Manifest: Docs/release-freeze-manifest-v0.18.0-build18.md")
print(f"Release status: {release_status}")
print(f"Current SHA-256: {current_sha or 'not available'}")
if blocked_reason:
    print(f"Blocked reason: {blocked_reason}")
print(f"Validation JSON: {safe_path(validation_path)}")

if status.startswith("FREEZE_MANIFEST_BLOCKED"):
    raise SystemExit(1)
PY

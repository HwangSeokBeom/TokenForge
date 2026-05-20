#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
FINAL_DISTRIBUTION="${RELEASE_DIR}/final-distribution-readiness.json"
ASSET_VALIDATION="${RELEASE_DIR}/github-release-assets-validation.json"
PLAN_JSON="${EVIDENCE_DIR}/github-release-publication-plan.json"
DRAFT_MD="${EVIDENCE_DIR}/github-release-draft.md"
EVIDENCE_LOCK="${EVIDENCE_DIR}/evidence-lock-summary.json"
FINAL_PRIVACY_AUDIT="${EVIDENCE_DIR}/final-privacy-regression-audit.json"
OUTPUT_JSON="${RELEASE_DIR}/github-release-publication-summary.json"
PUBLISH=false
YES=false
UPDATE_EXISTING=false
SKIP_PREP_CHECK="${TOKENFORGE_RELEASE_PUBLISH_SKIP_PREP_CHECK:-false}"

for arg in "$@"; do
  case "${arg}" in
    --publish)
      PUBLISH=true
      ;;
    --yes)
      YES=true
      ;;
    --update-existing)
      UPDATE_EXISTING=true
      ;;
    --help|-h)
      echo "Usage: scripts/publish-github-release.sh [--publish --yes] [--update-existing]"
      echo "Default mode is a dry-run that never publishes and does not require GitHub credentials."
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      exit 1
      ;;
  esac
done

if [[ "${PUBLISH}" == "true" && "${YES}" != "true" ]]; then
  echo "--publish requires --yes." >&2
  exit 1
fi

mkdir -p "${RELEASE_DIR}" "${EVIDENCE_DIR}"

if [[ ! -f "${ASSET_VALIDATION}" ]]; then
  PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${SCRIPT_DIR}/validate-github-release-assets.sh" >/dev/null || true
fi
if [[ ! -f "${PLAN_JSON}" ]]; then
  PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${SCRIPT_DIR}/generate-github-release-publication-plan.sh" >/dev/null || true
fi

write_summary() {
  local status="$1"
  local reason="$2"
  local url="${3:-}"
  /usr/bin/python3 - "${OUTPUT_JSON}" "${FINAL_DISTRIBUTION}" "${ASSET_VALIDATION}" "${PLAN_JSON}" "${EVIDENCE_LOCK}" "${FINAL_PRIVACY_AUDIT}" "${PACKAGE_PATH}" "${status}" "${reason}" "${url}" "${PUBLISH}" "${UPDATE_EXISTING}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

(
    output_json,
    final_distribution_path,
    validation_path,
    plan_path,
    lock_path,
    privacy_path,
    package_path,
    status,
    reason,
    url,
    publish_flag,
    update_existing,
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


def load_json(path):
    if not os.path.exists(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data if isinstance(data, dict) else {}
    except Exception:
        return {}


def safe_path(value):
    if isinstance(value, str) and (value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/")):
        return value
    return "controlled-path" if value else ""


def sha256(path):
    if not os.path.isfile(path):
        return ""
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


final_distribution = load_json(final_distribution_path)
validation = load_json(validation_path)
plan = load_json(plan_path)
lock = load_json(lock_path)
privacy = load_json(privacy_path)
safe_url = url if re.match(r"^https://[A-Za-z0-9./_?=&%#:-]+$", url or "") else ""
summary = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.githubReleasePublicationSummary.v1",
    "status": status,
    "publishRequested": publish_flag == "true",
    "updateExistingRequested": update_existing == "true",
    "releaseTitle": plan.get("releaseTitle", ""),
    "tag": plan.get("tag", ""),
    "releaseCandidateAssetFilename": os.path.basename(package_path),
    "releaseCandidateAssetPath": safe_path(package_path),
    "releaseCandidateSha256": validation.get("releaseCandidateSha256") or sha256(package_path),
    "evidenceArchiveFilename": validation.get("evidenceArchiveFilename", ""),
    "evidenceArchivePath": validation.get("evidenceArchivePath", ""),
    "evidenceArchiveSha256": validation.get("evidenceArchiveSha256", ""),
    "finalDistributionStatus": final_distribution.get("status", "missing"),
    "assetValidationStatus": validation.get("status", "missing"),
    "evidenceLockStatus": lock.get("status", "missing"),
    "finalPrivacyAuditStatus": privacy.get("status", "missing"),
    "releaseUrl": safe_url,
    "safeFailureReason": reason,
    "publishedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ") if status == "GITHUB_RELEASE_PUBLISHED" else "",
    "nextAction": "Generate post-release audit." if status in {"GITHUB_RELEASE_PUBLISHED", "GITHUB_RELEASE_ALREADY_EXISTS"} else "Resolve publication blockers before publishing.",
    "generatedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}
text = json.dumps(summary, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(text):
    raise SystemExit("GitHub Release publication summary contains unsafe content.")
with open(output_json, "w", encoding="utf-8") as handle:
    handle.write(text)
PY
}

json_get() {
  local path="$1"
  local expr="$2"
  /usr/bin/python3 - "$path" "$expr" <<'PY'
import json
import os
import sys
path, expr = sys.argv[1:]
if not os.path.exists(path):
    print("")
    raise SystemExit(0)
with open(path, "r", encoding="utf-8") as handle:
    data = json.load(handle)
value = data
for part in expr.split("."):
    if not part:
        continue
    value = value.get(part, "") if isinstance(value, dict) else ""
print(value if value is not None else "")
PY
}

TAG="v${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}"
TITLE="TokenForge macOS ${EXPECTED_VERSION} build ${EXPECTED_BUILD_NUMBER}"
ASSET_STATUS="$(json_get "${ASSET_VALIDATION}" "status")"
FINAL_STATUS="$(json_get "${FINAL_DISTRIBUTION}" "status")"
PLAN_STATUS="$(json_get "${PLAN_JSON}" "status")"
LOCK_STATUS="$(json_get "${EVIDENCE_LOCK}" "status")"
PRIVACY_STATUS="$(json_get "${FINAL_PRIVACY_AUDIT}" "status")"
EVIDENCE_PATH="$(json_get "${ASSET_VALIDATION}" "evidenceArchivePath")"

blocked_reasons=()
[[ "${FINAL_STATUS}" == "FINAL_DISTRIBUTION_READY" ]] || blocked_reasons+=("Final distribution readiness is ${FINAL_STATUS:-missing}.")
[[ "${ASSET_STATUS}" == "GITHUB_RELEASE_ASSETS_READY" ]] || blocked_reasons+=("GitHub Release assets are ${ASSET_STATUS:-missing}.")
[[ "${LOCK_STATUS}" == "EVIDENCE_LOCK_READY" ]] || blocked_reasons+=("Evidence lock is ${LOCK_STATUS:-missing}.")
[[ "${PRIVACY_STATUS}" == "FINAL_PRIVACY_AUDIT_PASSED" ]] || blocked_reasons+=("Final privacy audit is ${PRIVACY_STATUS:-missing}.")
[[ -f "${PACKAGE_PATH}" ]] || blocked_reasons+=("Release candidate zip is missing.")
[[ -f "${DRAFT_MD}" ]] || blocked_reasons+=("GitHub Release draft markdown is missing.")

cd "${REPO_ROOT}"
if git rev-parse -q --verify "refs/tags/${TAG}" >/dev/null; then
  TAG_EXISTS=true
else
  TAG_EXISTS=false
fi
[[ "${TAG_EXISTS}" == "true" ]] || blocked_reasons+=("Local release tag ${TAG} is missing.")

if [[ "${PUBLISH}" != "true" ]]; then
  if [[ "${#blocked_reasons[@]}" -eq 0 && "${PLAN_STATUS}" == "GITHUB_RELEASE_PUBLICATION_PLAN_READY" ]]; then
    status="GITHUB_RELEASE_DRY_RUN_READY"
    reason="Dry-run only; no GitHub Release was created."
  else
    status="GITHUB_RELEASE_DRY_RUN_BLOCKED"
    reason="$(IFS=' '; echo "${blocked_reasons[*]:-Publication plan is not ready.}")"
  fi
  write_summary "${status}" "${reason}"
  echo "TokenForge GitHub Release publish dry-run"
  echo "Status: ${status}"
  echo "Tag: ${TAG}"
  echo "Release candidate: ${PACKAGE_PATH}"
  echo "GitHub Release title: ${TITLE}"
  if [[ -n "${EVIDENCE_PATH}" ]]; then
    echo "Evidence archive: ${EVIDENCE_PATH}"
  fi
  if [[ "${#blocked_reasons[@]}" -gt 0 ]]; then
    echo "Publication blocked:"
    printf -- '- %s\n' "${blocked_reasons[@]}"
  fi
  echo "Summary: ${OUTPUT_JSON}"
  exit 0
fi

if [[ "${#blocked_reasons[@]}" -gt 0 ]]; then
  write_summary "GITHUB_RELEASE_BLOCKED" "$(IFS=' '; echo "${blocked_reasons[*]}")"
  echo "GITHUB_RELEASE_BLOCKED" >&2
  printf -- '- %s\n' "${blocked_reasons[@]}" >&2
  exit 1
fi

if [[ "${SKIP_PREP_CHECK}" != "true" ]]; then
  "${SCRIPT_DIR}/release-prep-check.sh" --summary-only >/dev/null
fi

if ! command -v gh >/dev/null 2>&1; then
  write_summary "GITHUB_RELEASE_BLOCKED" "gh CLI is not available."
  echo "GITHUB_RELEASE_BLOCKED: gh CLI is not available." >&2
  exit 1
fi
if ! gh auth status >/dev/null 2>&1; then
  write_summary "GITHUB_RELEASE_BLOCKED" "gh CLI is not authenticated."
  echo "GITHUB_RELEASE_BLOCKED: gh CLI is not authenticated." >&2
  exit 1
fi

release_url=""
if gh release view "${TAG}" --json url --jq '.url' >/tmp/tokenforge-gh-release-url.txt 2>/dev/null; then
  release_url="$(head -n 1 /tmp/tokenforge-gh-release-url.txt | tr -d '\r\n')"
  if [[ "${UPDATE_EXISTING}" != "true" ]]; then
    write_summary "GITHUB_RELEASE_ALREADY_EXISTS" "Release already exists; no overwrite was attempted." "${release_url}"
    echo "GITHUB_RELEASE_ALREADY_EXISTS"
    echo "Summary: ${OUTPUT_JSON}"
    exit 0
  fi
  existing_assets="$(gh release view "${TAG}" --json assets --jq '.assets[].name' 2>/dev/null || true)"
  if ! grep -Fxq "$(basename "${PACKAGE_PATH}")" <<<"${existing_assets}"; then
    gh release upload "${TAG}" "${PACKAGE_PATH}" >/dev/null
  fi
  if [[ -n "${EVIDENCE_PATH}" && -f "${EVIDENCE_PATH}" ]] && ! grep -Fxq "$(basename "${EVIDENCE_PATH}")" <<<"${existing_assets}"; then
    gh release upload "${TAG}" "${EVIDENCE_PATH}" >/dev/null
  fi
  gh release edit "${TAG}" --title "${TITLE}" --notes-file "${DRAFT_MD}" >/dev/null
  write_summary "GITHUB_RELEASE_PUBLISHED" "Existing release updated safely." "${release_url}"
  echo "GITHUB_RELEASE_PUBLISHED"
  echo "Summary: ${OUTPUT_JSON}"
  exit 0
fi

assets=("${PACKAGE_PATH}")
if [[ -n "${EVIDENCE_PATH}" && -f "${EVIDENCE_PATH}" ]]; then
  assets+=("${EVIDENCE_PATH}")
fi
if gh release create "${TAG}" "${assets[@]}" --title "${TITLE}" --notes-file "${DRAFT_MD}" >/tmp/tokenforge-gh-release-create-url.txt 2>/dev/null; then
  release_url="$(head -n 1 /tmp/tokenforge-gh-release-create-url.txt | tr -d '\r\n')"
  write_summary "GITHUB_RELEASE_PUBLISHED" "GitHub Release create command succeeded." "${release_url}"
  echo "GITHUB_RELEASE_PUBLISHED"
  echo "Summary: ${OUTPUT_JSON}"
else
  write_summary "GITHUB_RELEASE_FAILED" "gh release create failed; raw gh output was not stored."
  echo "GITHUB_RELEASE_FAILED: gh release create failed; raw gh output was not stored." >&2
  exit 1
fi

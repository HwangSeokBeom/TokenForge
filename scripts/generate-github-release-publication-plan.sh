#!/usr/bin/env bash
set -euo pipefail

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
EVIDENCE_DIR="${TOKENFORGE_RELEASE_EVIDENCE_DIR:-${RELEASE_DIR}/evidence}"
PACKAGE_PATH="${PACKAGE_PATH:-${RELEASE_DIR}/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
FINAL_DISTRIBUTION="${RELEASE_DIR}/final-distribution-readiness.json"
ASSET_VALIDATION="${RELEASE_DIR}/github-release-assets-validation.json"
DRAFT_MD="${EVIDENCE_DIR}/github-release-draft.md"
DRAFT_JSON="${EVIDENCE_DIR}/github-release-draft.json"
BUNDLE_INDEX="${EVIDENCE_DIR}/release-bundle-index.json"
EVIDENCE_LOCK="${EVIDENCE_DIR}/evidence-lock-summary.json"
PLAN_MD="${EVIDENCE_DIR}/github-release-publication-plan.md"
PLAN_JSON="${EVIDENCE_DIR}/github-release-publication-plan.json"
TARGET_BRANCH="${TOKENFORGE_RELEASE_TARGET_BRANCH:-}"

mkdir -p "${RELEASE_DIR}" "${EVIDENCE_DIR}"

if [[ ! -f "${ASSET_VALIDATION}" ]]; then
  PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_EVIDENCE_DIR="${EVIDENCE_DIR}" "${BASH_SOURCE%/*}/validate-github-release-assets.sh" >/dev/null || true
fi

/usr/bin/python3 - "${PLAN_MD}" "${PLAN_JSON}" "${PACKAGE_PATH}" "${FINAL_DISTRIBUTION}" "${ASSET_VALIDATION}" "${DRAFT_MD}" "${DRAFT_JSON}" "${BUNDLE_INDEX}" "${EVIDENCE_LOCK}" "${EXPECTED_VERSION}" "${EXPECTED_BUILD_NUMBER}" "${TARGET_BRANCH}" <<'PY'
import datetime
import hashlib
import json
import os
import re
import sys

(
    plan_md,
    plan_json,
    package_path,
    final_distribution_path,
    validation_path,
    draft_md_path,
    draft_json_path,
    bundle_path,
    lock_path,
    version,
    build,
    target_branch,
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


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def safe_path(value):
    if isinstance(value, str) and (value.startswith("/tmp/tokenforge-release/") or value.startswith("/tmp/tokenforge-macos-build/")):
        return value
    return "controlled-path" if value else ""


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


final_distribution = load_json(final_distribution_path)
validation = load_json(validation_path)
draft = load_json(draft_json_path)
bundle = load_json(bundle_path)
lock = load_json(lock_path)

tag = f"v{version}-build{build}"
title = f"TokenForge macOS {version} build {build}"
asset_status = validation.get("status", "missing")
final_status = final_distribution.get("status", "missing")
package_sha = validation.get("releaseCandidateSha256") or final_distribution.get("releaseCandidateSha256") or draft.get("releaseCandidateSha256") or bundle.get("releaseCandidateSha256") or sha256(package_path)
evidence_path = validation.get("evidenceArchivePath") or (lock.get("evidenceArchivePath") if lock.get("status") == "EVIDENCE_LOCK_READY" else "")
evidence_sha = validation.get("evidenceArchiveSha256") or (lock.get("evidenceArchiveSha256") if lock.get("status") == "EVIDENCE_LOCK_READY" else "")
evidence_safe = bool(evidence_path and evidence_sha and asset_status == "GITHUB_RELEASE_ASSETS_READY")
target_branch_status = "configured" if target_branch else "not recorded; configure TOKENFORGE_RELEASE_TARGET_BRANCH before publication if a target branch must be documented"
target_branch_value = target_branch if target_branch else ""
if target_branch_value and UNSAFE.search(target_branch_value):
    raise SystemExit("Configured target branch contains unsafe content.")

blocked = []
if final_status != "FINAL_DISTRIBUTION_READY":
    blocked.append("Final distribution readiness is not ready.")
if asset_status != "GITHUB_RELEASE_ASSETS_READY":
    blocked.append(f"GitHub Release assets are {asset_status}.")
if not os.path.exists(package_path):
    blocked.append("Release candidate asset is missing.")
if not os.path.exists(draft_md_path):
    blocked.append("GitHub Release draft markdown is missing.")

publication_allowed = not blocked
publication_status = "GITHUB_RELEASE_PUBLICATION_PLAN_READY" if publication_allowed else "GITHUB_RELEASE_PUBLICATION_PLAN_BLOCKED"
create_command = (
    f'gh release create {tag} {safe_path(package_path)} --title "{title}" --notes-file {safe_path(draft_md_path)}'
)
evidence_command = (
    f"gh release upload {tag} {safe_path(evidence_path)}"
    if evidence_safe
    else ""
)

steps = [
    "Confirm final distribution readiness is FINAL_DISTRIBUTION_READY.",
    "Confirm GitHub Release assets validation is GITHUB_RELEASE_ASSETS_READY.",
    "Confirm the guarded local release tag exists and points to the current release checkout.",
    "Run the publish script without flags and review the dry-run summary.",
    "Run the guarded publish command only after the final checks pass.",
    "Generate the post-release audit and status dashboard after publication or a safe block.",
]

plan = {
    "schemaVersion": 1,
    "schemaName": "tokenforge.githubReleasePublicationPlan.v1",
    "status": publication_status,
    "releaseTitle": title,
    "tag": tag,
    "targetBranchStatus": target_branch_status,
    "targetBranch": target_branch_value,
    "releaseCandidateAssetFilename": os.path.basename(package_path),
    "releaseCandidateAssetPath": safe_path(package_path),
    "releaseCandidateSha256": package_sha,
    "evidenceArchiveFilename": os.path.basename(evidence_path) if evidence_path else "",
    "evidenceArchivePath": safe_path(evidence_path) if evidence_path else "",
    "evidenceArchiveSha256": evidence_sha if evidence_path else "",
    "githubReleaseDraftPath": safe_path(draft_md_path),
    "assetValidationStatus": asset_status,
    "finalDistributionStatus": final_status,
    "manualPublicationSteps": steps,
    "suggestedCreateCommand": create_command,
    "suggestedEvidenceUploadCommand": evidence_command,
    "blockedReasons": sorted(set(blocked)),
    "warning": "Suggested gh commands must only be run after final checks pass; this plan does not publish.",
    "generatedAt": utc_now(),
}

json_text = json.dumps(plan, indent=2, sort_keys=True) + "\n"
if UNSAFE.search(json_text):
    raise SystemExit("GitHub Release publication plan JSON contains unsafe content.")
with open(plan_json, "w", encoding="utf-8") as handle:
    handle.write(json_text)

lines = [
    "# TokenForge GitHub Release Publication Plan",
    "",
    f"- Release title: {title}",
    f"- Tag: `{tag}`",
    f"- Target branch: {target_branch_value if target_branch_value else target_branch_status}",
    f"- Release candidate asset: `{os.path.basename(package_path)}`",
    f"- Release candidate SHA-256: `{package_sha or 'not available'}`",
    f"- GitHub Release draft: `{safe_path(draft_md_path)}`",
    f"- Asset validation status: `{asset_status}`",
    f"- Final distribution status: `{final_status}`",
    f"- Publication plan status: `{publication_status}`",
]
if evidence_path:
    lines.extend([
        f"- Evidence archive: `{os.path.basename(evidence_path)}`",
        f"- Evidence archive SHA-256: `{evidence_sha or 'not available'}`",
    ])
lines.extend([
    "",
    "This plan is non-publishing. The suggested commands are examples only and should be run only after final checks pass.",
    "",
    "## Manual Publication Steps",
])
lines.extend(f"{index}. {step}" for index, step in enumerate(steps, start=1))
lines.extend([
    "",
    "## Suggested gh Commands",
    "",
    "```bash",
    create_command,
])
if evidence_command:
    lines.append(evidence_command)
lines.extend([
    "```",
    "",
    "## Blocked Reasons",
])
if blocked:
    lines.extend(f"- {reason}" for reason in sorted(set(blocked)))
else:
    lines.append("- None.")
md_text = "\n".join(lines) + "\n"
if UNSAFE.search(md_text):
    raise SystemExit("GitHub Release publication plan markdown contains unsafe content.")
with open(plan_md, "w", encoding="utf-8") as handle:
    handle.write(md_text)

print("TokenForge GitHub Release publication plan")
print(f"Status: {publication_status}")
print(f"Tag: {tag}")
print(f"Asset validation: {asset_status}")
print(f"Final distribution: {final_status}")
if blocked:
    print("Publication blocked:")
    for reason in sorted(set(blocked)):
        print(f"- {reason}")
print(f"Markdown: {safe_path(plan_md)}")
print(f"JSON: {safe_path(plan_json)}")
PY

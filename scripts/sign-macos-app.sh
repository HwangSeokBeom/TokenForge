#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
SIGN_MODE="${TOKENFORGE_RELEASE_SIGN_MODE:-${TOKENFORGE_MACOS_SIGN_MODE:-adhoc}}"
SIGN_IDENTITY="${TOKENFORGE_MACOS_SIGN_IDENTITY:-}"
BUNDLE_ID="${TOKENFORGE_MACOS_BUNDLE_ID:-com.tokenforge.client}"
ENTITLEMENTS_PATH="${TOKENFORGE_MACOS_ENTITLEMENTS_PATH:-${REPO_ROOT}/BuildSupport/macOS/TokenForge.entitlements}"
DRY_RUN="${TOKENFORGE_SIGN_DRY_RUN:-false}"
NOTARIZE_ENABLED="${TOKENFORGE_RELEASE_NOTARIZE:-false}"

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  echo "Usage: APP_PATH=/path/TokenForge.app TOKENFORGE_RELEASE_SIGN_MODE=adhoc|developer-id scripts/sign-macos-app.sh"
  echo "Developer ID mode requires a configured signing identity. Entitlements default to BuildSupport/macOS/TokenForge.entitlements."
  exit 0
fi

if [[ ! -d "${APP_PATH}" ]]; then
  echo "TokenForge app bundle was not found. Set APP_PATH to a built .app bundle." >&2
  exit 1
fi

if ! command -v codesign >/dev/null 2>&1; then
  echo "codesign is not available on this machine." >&2
  exit 1
fi

if [[ ! -f "${ENTITLEMENTS_PATH}" ]]; then
  echo "TokenForge macOS entitlements file was not found. Set TOKENFORGE_MACOS_ENTITLEMENTS_PATH." >&2
  exit 1
fi

if [[ -z "${BUNDLE_ID}" ]]; then
  echo "TokenForge macOS bundle identifier is required." >&2
  exit 1
fi

if [[ "${SIGN_MODE}" == "developer-id" ]]; then
  if [[ -z "${SIGN_IDENTITY}" ]]; then
    echo "Developer ID signing requires a configured signing identity." >&2
    exit 1
  fi
  if ! command -v security >/dev/null 2>&1; then
    echo "macOS security tool is not available; cannot validate Developer ID signing identity." >&2
    exit 1
  fi
  if ! security find-identity -p codesigning -v 2>/dev/null | grep -Fq "${SIGN_IDENTITY}"; then
    echo "Requested Developer ID signing identity was not found in the local keychain." >&2
    exit 1
  fi
elif [[ "${SIGN_MODE}" == "adhoc" ]]; then
  if [[ "${NOTARIZE_ENABLED}" == "true" ]]; then
    echo "Notarization requires Developer ID signing; ad-hoc signing cannot be notarized." >&2
    exit 1
  fi
  SIGN_IDENTITY="${SIGN_IDENTITY:--}"
else
  echo "TOKENFORGE_RELEASE_SIGN_MODE must be 'adhoc' or 'developer-id'." >&2
  exit 1
fi

find "${APP_PATH}" -name .DS_Store -delete
if [[ "${TOKENFORGE_VERBOSE:-0}" == "1" ]]; then
  echo "TokenForge macOS xattr audit before signing"
  xattr -lr "${APP_PATH}" 2>/dev/null || true
else
  echo "TokenForge macOS xattr audit before signing: suppressed (set TOKENFORGE_VERBOSE=1 for full listing)"
fi

remove_xattr_name() {
  local name="$1"
  while IFS= read -r item; do
    if ! xattr -d "${name}" "${item}" 2>/dev/null; then
      echo "WARN failed to remove xattr '${name}' from ${item}" >&2
    fi
  done < <(find "${APP_PATH}" -xattrname "${name}" -print 2>/dev/null || true)
}

xattr -cr "${APP_PATH}" 2>/dev/null || true
xattr -c "${APP_PATH}" 2>/dev/null || true
remove_xattr_name com.apple.quarantine
remove_xattr_name com.apple.FinderInfo
remove_xattr_name com.apple.ResourceFork
remove_xattr_name 'com.apple.fileprovider.fpfs#P'
remove_xattr_name 'com.apple.fileprovider.fpfs#C'
remove_xattr_name com.apple.provenance

remaining_xattrs="$(xattr -lr "${APP_PATH}" 2>/dev/null || true)"
if [[ -n "${remaining_xattrs}" ]]; then
  echo "TokenForge macOS xattr audit after cleanup: extended attributes remain"
  if [[ "${TOKENFORGE_VERBOSE:-0}" == "1" ]]; then
    echo "${remaining_xattrs}"
  else
    echo "TokenForge macOS xattr audit after cleanup: listing suppressed (set TOKENFORGE_VERBOSE=1 for full listing)"
  fi
  if echo "${remaining_xattrs}" | grep -E 'com.apple.(quarantine|FinderInfo|ResourceFork|fileprovider)' >/dev/null; then
    echo "WARN disallowed signing xattrs remain after cleanup; codesign may reject this bundle." >&2
  fi
fi

codesign_args=(--force --deep --entitlements "${ENTITLEMENTS_PATH}" --identifier "${BUNDLE_ID}" --sign "${SIGN_IDENTITY}")

if [[ "${SIGN_MODE}" == "developer-id" ]]; then
  codesign_args+=(--options runtime)
fi

if [[ "${SIGN_IDENTITY}" != "-" ]]; then
  codesign_args+=(--timestamp)
fi

if [[ "${DRY_RUN}" == "true" ]]; then
  echo "TokenForge macOS signing summary"
  echo "Mode: ${SIGN_MODE}"
  echo "App path: ${APP_PATH}"
  echo "Bundle identifier: ${BUNDLE_ID}"
  echo "Entitlements: present"
  echo "Result: dry-run"
  exit 0
fi

codesign "${codesign_args[@]}" "${APP_PATH}"
codesign --verify --deep --strict --verbose=4 "${APP_PATH}"
codesign -dv --verbose=4 "${APP_PATH}" 2>&1 || true
codesign -d --entitlements :- "${APP_PATH}" >/dev/null 2>&1 || true

echo "TokenForge macOS signing summary"
echo "Mode: ${SIGN_MODE}"
echo "App path: ${APP_PATH}"
echo "Bundle identifier: ${BUNDLE_ID}"
if [[ "${SIGN_MODE}" == "developer-id" ]]; then
  echo "Identity: ${SIGN_IDENTITY}"
else
  echo "Identity: ad-hoc"
fi
echo "Entitlements: present"
if [[ "${SIGN_MODE}" == "developer-id" ]]; then
  echo "Hardened runtime: enabled"
else
  echo "Hardened runtime: disabled for local ad-hoc player validation"
fi
echo "Result: signed and verified"

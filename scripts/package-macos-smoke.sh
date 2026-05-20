#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
PACKAGE_DIR="${PACKAGE_DIR:-/tmp/tokenforge-release}"
PACKAGE_PATH="${PACKAGE_PATH:-${PACKAGE_DIR}/TokenForge.zip}"
RUN_BUILD="${TOKENFORGE_PACKAGE_RUN_BUILD:-true}"
ADHOC_SIGN="${TOKENFORGE_PACKAGE_ADHOC_SIGN:-true}"
EXPECTED_BUNDLE_ID="${TOKENFORGE_EXPECTED_BUNDLE_ID:-com.tokenforge.client}"
EXPECTED_PRODUCT_NAME="${TOKENFORGE_EXPECTED_PRODUCT_NAME:-TokenForge}"
EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"

if [[ ! -d "${APP_PATH}" ]]; then
  if [[ "${RUN_BUILD}" == "true" ]]; then
    "${SCRIPT_DIR}/build-macos-smoke.sh"
  else
    echo "TokenForge app bundle was not found. Set APP_PATH or enable TOKENFORGE_PACKAGE_RUN_BUILD=true." >&2
    exit 1
  fi
fi

if [[ ! -d "${APP_PATH}" ]]; then
  echo "TokenForge app bundle was not produced." >&2
  exit 1
fi

if [[ "${ADHOC_SIGN}" == "true" ]]; then
  TOKENFORGE_MACOS_SIGN_MODE=adhoc TOKENFORGE_MACOS_SIGN_IDENTITY="-" APP_PATH="${APP_PATH}" "${SCRIPT_DIR}/sign-macos-app.sh"
  TOKENFORGE_MACOS_STRICT_SPCTL=false APP_PATH="${APP_PATH}" "${SCRIPT_DIR}/verify-macos-signing.sh"
fi

rm -rf "${PACKAGE_DIR}"
mkdir -p "${PACKAGE_DIR}"
ditto -c -k --sequesterRsrc --keepParent "${APP_PATH}" "${PACKAGE_PATH}"

zip_listing="$(zipinfo -1 "${PACKAGE_PATH}")"
if ! grep -q '^TokenForge.app/' <<<"${zip_listing}"; then
  echo "Package does not contain TokenForge.app." >&2
  exit 1
fi

if ! grep -q '^TokenForge.app/Contents/Info.plist$' <<<"${zip_listing}"; then
  echo "Package does not contain app Info.plist." >&2
  exit 1
fi

if ! grep -q '^TokenForge.app/Contents/MacOS/' <<<"${zip_listing}"; then
  echo "Package does not contain app executable directory." >&2
  exit 1
fi

for forbidden in \
  "tokenforge-approved-locations.local.json" \
  "artifacts/client-contract/" \
  "tokenforge-session" \
  "tokenforge-token" \
  "tokenforge-sync-retry-queue.local.json" \
  "tokenforge-sync-tombstones.local.json" \
  "tokenforge-sync-conflicts.local.json" \
  "tokenforge-sync-conflict-audit.local.json" \
  "tokenforge-sync-local-state.local.json" \
  "tokenforge-sync-state" \
  "local-delete-debug" \
  "raw-sync-payload" \
  ".tokens.local.json" \
  ".session.local.json"; do
  if grep -q "${forbidden}" <<<"${zip_listing}"; then
    echo "Package contains a forbidden local/generated artifact." >&2
    exit 1
  fi
done

VERIFY_DIR="$(mktemp -d /tmp/tokenforge-package-smoke.XXXXXX)"
unzip -q "${PACKAGE_PATH}" -d "${VERIFY_DIR}"
INFO_PLIST="${VERIFY_DIR}/TokenForge.app/Contents/Info.plist"
if [[ ! -f "${INFO_PLIST}" ]]; then
  echo "Unzipped package is missing Info.plist." >&2
  rm -rf "${VERIFY_DIR}"
  exit 1
fi

plist_value() {
  /usr/libexec/PlistBuddy -c "Print :$1" "${INFO_PLIST}" 2>/dev/null || true
}

bundle_id="$(plist_value CFBundleIdentifier)"
bundle_name="$(plist_value CFBundleName)"
display_name="$(plist_value CFBundleDisplayName)"
short_version="$(plist_value CFBundleShortVersionString)"
build_number="$(plist_value CFBundleVersion)"
executable_name="$(plist_value CFBundleExecutable)"
product_name="${display_name:-${bundle_name}}"

if [[ "${bundle_id}" != "${EXPECTED_BUNDLE_ID}" ]]; then
  echo "Package Info.plist has unexpected bundle identifier." >&2
  rm -rf "${VERIFY_DIR}"
  exit 1
fi

if [[ "${product_name}" != "${EXPECTED_PRODUCT_NAME}" ]]; then
  echo "Package Info.plist has unexpected product name." >&2
  rm -rf "${VERIFY_DIR}"
  exit 1
fi

if [[ "${short_version}" != "${EXPECTED_VERSION}" ]]; then
  echo "Package Info.plist has unexpected app version." >&2
  rm -rf "${VERIFY_DIR}"
  exit 1
fi

if [[ "${build_number}" != "${EXPECTED_BUILD_NUMBER}" ]]; then
  echo "Package Info.plist has unexpected build number." >&2
  rm -rf "${VERIFY_DIR}"
  exit 1
fi

if [[ -z "${executable_name}" ]] || [[ ! -x "${VERIFY_DIR}/TokenForge.app/Contents/MacOS/${executable_name}" ]]; then
  echo "Package executable is missing or not executable." >&2
  rm -rf "${VERIFY_DIR}"
  exit 1
fi

while IFS= read -r -d '' log_file; do
  if grep -Eiq 'APPLE_ID|APP_SPECIFIC_PASSWORD|TEAM_ID|TOKENFORGE_MACOS_SIGN_IDENTITY|NOTARYTOOL_KEYCHAIN_PROFILE|bearer[[:space:]]+[a-z0-9._-]+|api[_-]?key[[:space:]]*[:=]|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|tokenforge-approved-locations\.local\.json|tokenforge-session|tokenforge-token|tokenforge-sync-retry-queue\.local\.json|tokenforge-sync-tombstones\.local\.json|tokenforge-sync-conflicts\.local\.json|tokenforge-sync-conflict-audit\.local\.json|tokenforge-sync-local-state\.local\.json|raw[ _-]?sync[ _-]?payload|local[ _-]?delete[ _-]?debug|retry[ _-]?backup|recovery[ _-]?sync[ _-]?state' "${log_file}"; then
    echo "Package contains a log with sensitive markers." >&2
    rm -rf "${VERIFY_DIR}"
    exit 1
  fi
done < <(find "${VERIFY_DIR}/TokenForge.app" -type f -name '*.log' -print0)

rm -rf "${VERIFY_DIR}"

echo "TokenForge macOS package smoke summary"
echo "App path: ${APP_PATH}"
echo "Package path: ${PACKAGE_PATH}"
echo "Bundle identifier: ${EXPECTED_BUNDLE_ID}"
echo "Product name: ${EXPECTED_PRODUCT_NAME}"
echo "Version: ${EXPECTED_VERSION}"
echo "Build number: ${EXPECTED_BUILD_NUMBER}"
echo "Ad-hoc signed: ${ADHOC_SIGN}"
echo "Notarized: false"
echo "Result: packaged and verified"

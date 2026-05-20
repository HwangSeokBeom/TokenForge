#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

EXPECTED_PRODUCT_NAME="${TOKENFORGE_EXPECTED_PRODUCT_NAME:-TokenForge}"
EXPECTED_BUNDLE_ID="${TOKENFORGE_MACOS_BUNDLE_ID:-com.tokenforge.client}"
EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
RELEASE_DIR="${TOKENFORGE_RELEASE_DIR:-/tmp/tokenforge-release}"
RUN_BUILD="${TOKENFORGE_RELEASE_RUN_BUILD:-true}"
SIGN_MODE="${TOKENFORGE_RELEASE_SIGN_MODE:-adhoc}"
NOTARIZE="${TOKENFORGE_RELEASE_NOTARIZE:-false}"
ALLOW_ADHOC="${TOKENFORGE_RELEASE_ALLOW_ADHOC:-true}"
DEFAULT_PACKAGE_BASENAME="TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip"
DEFAULT_NOTARIZED_PACKAGE_BASENAME="TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}-notarized.zip"
if [[ -n "${PACKAGE_PATH:-}" ]]; then
  PACKAGE_PATH="${PACKAGE_PATH}"
elif [[ "${NOTARIZE}" == "true" ]]; then
  PACKAGE_PATH="${RELEASE_DIR}/${DEFAULT_NOTARIZED_PACKAGE_BASENAME}"
else
  PACKAGE_PATH="${RELEASE_DIR}/${DEFAULT_PACKAGE_BASENAME}"
fi

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  echo "Usage: TOKENFORGE_RELEASE_SIGN_MODE=adhoc|developer-id TOKENFORGE_RELEASE_NOTARIZE=false scripts/package-macos-release-candidate.sh"
  echo "Creates a versioned macOS release candidate zip and runs metadata, signing, package, and privacy checks."
  exit 0
fi

if [[ "${SIGN_MODE}" != "adhoc" && "${SIGN_MODE}" != "developer-id" ]]; then
  echo "Release sign mode must be adhoc or developer-id." >&2
  exit 1
fi

if [[ "${SIGN_MODE}" == "adhoc" && "${ALLOW_ADHOC}" != "true" ]]; then
  echo "Ad-hoc release candidate signing is disabled for this run." >&2
  exit 1
fi

if [[ "${NOTARIZE}" == "true" && "${SIGN_MODE}" != "developer-id" ]]; then
  echo "Notarized release candidates require Developer ID signing." >&2
  exit 1
fi

if [[ -z "${PACKAGE_PATH}" || "${PACKAGE_PATH}" != *.zip ]]; then
  echo "Release candidate PACKAGE_PATH must be a .zip path." >&2
  exit 1
fi

"${SCRIPT_DIR}/validate-release-metadata.sh"

if [[ ! -d "${APP_PATH}" ]]; then
  if [[ "${RUN_BUILD}" == "true" ]]; then
    "${SCRIPT_DIR}/build-macos-smoke.sh"
  else
    echo "TokenForge app bundle was not found. Set APP_PATH or enable release build." >&2
    exit 1
  fi
fi

TOKENFORGE_RELEASE_SIGN_MODE="${SIGN_MODE}" \
TOKENFORGE_MACOS_BUNDLE_ID="${EXPECTED_BUNDLE_ID}" \
APP_PATH="${APP_PATH}" \
"${SCRIPT_DIR}/sign-macos-app.sh"

TOKENFORGE_MACOS_STRICT_SPCTL=false \
TOKENFORGE_MACOS_REQUIRE_RUNTIME=true \
TOKENFORGE_MACOS_BUNDLE_ID="${EXPECTED_BUNDLE_ID}" \
APP_PATH="${APP_PATH}" \
"${SCRIPT_DIR}/verify-macos-signing.sh"

require_in_package() {
  if ! grep -q "$1" <<<"${zip_listing}"; then
    echo "Release package is missing required app content." >&2
    exit 1
  fi
}

reject_in_package() {
  if grep -Eiq "$1" <<<"${zip_listing}"; then
    echo "Release package contains a forbidden local or generated artifact." >&2
    exit 1
  fi
}

package_app() {
  rm -f "${PACKAGE_PATH}"
  mkdir -p "$(dirname "${PACKAGE_PATH}")"
  ditto -c -k --sequesterRsrc --keepParent "${APP_PATH}" "${PACKAGE_PATH}"
}

verify_package() {
  if [[ ! -f "${PACKAGE_PATH}" ]]; then
    echo "Release package was not created." >&2
    exit 1
  fi

  zip_listing="$(zipinfo -1 "${PACKAGE_PATH}")"

  require_in_package '^TokenForge.app/$'
  require_in_package '^TokenForge.app/Contents/Info.plist$'
  require_in_package '^TokenForge.app/Contents/MacOS/'

  for forbidden in \
    'TokenForgePlaceholderIcon\.png' \
    'tokenforge-approved-locations\.local\.json' \
    'artifacts/client-contract/' \
    'tokenforge-session' \
    'tokenforge-token' \
    'tokenforge-sync-retry-queue\.local\.json' \
    'tokenforge-sync-tombstones\.local\.json' \
    'tokenforge-sync-conflicts\.local\.json' \
    'tokenforge-sync-conflict-audit\.local\.json' \
    'tokenforge-sync-local-state\.local\.json' \
    'tokenforge-sync-state.*\.local\.json' \
    'retry[ _-]?backup' \
    'recovery[ _-]?sync[ _-]?state' \
    'local[ _-]?delete[ _-]?debug' \
    '\.tokens\.local\.json' \
    '\.session\.local\.json' \
    '\.corrupt$' \
    '\.unsupported-schema$' \
    '\.unsafe$' \
    '\.log$' \
    '\.env$' \
    'APPLE_ID' \
    'APP_SPECIFIC_PASSWORD' \
    'TEAM_ID' \
    'NOTARYTOOL_KEYCHAIN_PROFILE' \
    'notary' \
    'credentials' \
    'Apple[ _-]?credentials' \
    'notary[ _-]?credentials' \
    'raw-sync-payload' \
    'raw[ _-]?sync[ _-]?payload' \
    'Tests/Fixtures/Persistence/' \
    'TokenForge\.Client\.Tests' \
    '\.Tests\.dll$' \
    '/Editor/' \
    '/scripts/' \
    '\.sh$'; do
    reject_in_package "${forbidden}"
  done

  VERIFY_DIR="$(mktemp -d /tmp/tokenforge-rc-package.XXXXXX)"
  unzip -q "${PACKAGE_PATH}" -d "${VERIFY_DIR}"
  INFO_PLIST="${VERIFY_DIR}/TokenForge.app/Contents/Info.plist"
  if [[ ! -f "${INFO_PLIST}" ]]; then
    echo "Release package is missing Info.plist after extraction." >&2
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
    echo "Release package Info.plist has unexpected bundle identifier." >&2
    rm -rf "${VERIFY_DIR}"
    exit 1
  fi

  if [[ "${product_name}" != "${EXPECTED_PRODUCT_NAME}" ]]; then
    echo "Release package Info.plist has unexpected product name." >&2
    rm -rf "${VERIFY_DIR}"
    exit 1
  fi

  if [[ "${short_version}" != "${EXPECTED_VERSION}" || "${build_number}" != "${EXPECTED_BUILD_NUMBER}" ]]; then
    echo "Release package Info.plist has unexpected version metadata." >&2
    rm -rf "${VERIFY_DIR}"
    exit 1
  fi

  if [[ -z "${executable_name}" || ! -x "${VERIFY_DIR}/TokenForge.app/Contents/MacOS/${executable_name}" ]]; then
    echo "Release package executable is missing or not executable." >&2
    rm -rf "${VERIFY_DIR}"
    exit 1
  fi

  if /usr/libexec/PlistBuddy -c "Print :CFBundleIdentifier" "${INFO_PLIST}" 2>/dev/null | grep -Eiq 'example|placeholder|dev'; then
    echo "Release package contains placeholder bundle metadata." >&2
    rm -rf "${VERIFY_DIR}"
    exit 1
  fi

  if grep -Eiq 'localhost:3000|127\.0\.0\.1|example|placeholder' "${INFO_PLIST}"; then
    echo "Release package Info.plist contains dev-only or placeholder metadata." >&2
    rm -rf "${VERIFY_DIR}"
    exit 1
  fi

  while IFS= read -r -d '' candidate; do
    if grep -Eiq 'APPLE_ID|APP_SPECIFIC_PASSWORD|TEAM_ID|TOKENFORGE_MACOS_SIGN_IDENTITY|NOTARYTOOL_KEYCHAIN_PROFILE|bearer[[:space:]]+[a-z0-9._-]+|api[_-]?key[[:space:]]*[:=]|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|tokenforge-approved-locations\.local\.json|tokenforge-session|tokenforge-token|tokenforge-sync-retry-queue\.local\.json|tokenforge-sync-tombstones\.local\.json|tokenforge-sync-conflicts\.local\.json|tokenforge-sync-conflict-audit\.local\.json|tokenforge-sync-local-state\.local\.json|raw[ _-]?sync[ _-]?payload|local[ _-]?delete[ _-]?debug|retry[ _-]?backup|recovery[ _-]?sync[ _-]?state' "${candidate}"; then
      echo "Release package contains a file with sensitive markers." >&2
      rm -rf "${VERIFY_DIR}"
      exit 1
    fi
  done < <(find "${VERIFY_DIR}/TokenForge.app" -type f \( -name '*.log' -o -name '*.json' -o -name '*.txt' -o -name '*.plist' \) -print0)

  if [[ "${NOTARIZE}" == "true" ]]; then
    TOKENFORGE_MACOS_STRICT_SPCTL=true \
    TOKENFORGE_MACOS_REQUIRE_RUNTIME=true \
    TOKENFORGE_MACOS_BUNDLE_ID="${EXPECTED_BUNDLE_ID}" \
    APP_PATH="${VERIFY_DIR}/TokenForge.app" \
    "${SCRIPT_DIR}/verify-macos-signing.sh"
  fi

  rm -rf "${VERIFY_DIR}"
}

rm -rf "${RELEASE_DIR}"
mkdir -p "${RELEASE_DIR}"
package_app
verify_package

if [[ "${NOTARIZE}" == "true" ]]; then
  PACKAGE_PATH="${PACKAGE_PATH}" TOKENFORGE_RELEASE_NOTARIZE=true "${SCRIPT_DIR}/notarize-macos-app.sh"
  APP_PATH="${APP_PATH}" TOKENFORGE_MACOS_STRICT_SPCTL=true "${SCRIPT_DIR}/staple-macos-app.sh"
  TOKENFORGE_MACOS_STRICT_SPCTL=true \
  TOKENFORGE_MACOS_REQUIRE_RUNTIME=true \
  TOKENFORGE_MACOS_BUNDLE_ID="${EXPECTED_BUNDLE_ID}" \
  APP_PATH="${APP_PATH}" \
  "${SCRIPT_DIR}/verify-macos-signing.sh"
  package_app
  verify_package
fi

echo "TokenForge macOS release candidate package summary"
echo "App path: ${APP_PATH}"
echo "Package path: ${PACKAGE_PATH}"
echo "Bundle identifier: ${EXPECTED_BUNDLE_ID}"
echo "Product name: ${EXPECTED_PRODUCT_NAME}"
echo "Version: ${EXPECTED_VERSION}"
echo "Build number: ${EXPECTED_BUILD_NUMBER}"
echo "Signing mode: ${SIGN_MODE}"
echo "Notarized: ${NOTARIZE}"
echo "Privacy scan: passed"
echo "Result: packaged and verified"

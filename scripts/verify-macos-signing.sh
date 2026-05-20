#!/usr/bin/env bash
set -euo pipefail

APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
STRICT_SPCTL="${TOKENFORGE_MACOS_STRICT_SPCTL:-false}"
EXPECTED_BUNDLE_ID="${TOKENFORGE_MACOS_BUNDLE_ID:-com.tokenforge.client}"
REQUIRE_RUNTIME="${TOKENFORGE_MACOS_REQUIRE_RUNTIME:-false}"

if [[ ! -d "${APP_PATH}" ]]; then
  echo "TokenForge app bundle was not found. Set APP_PATH to a built .app bundle." >&2
  exit 1
fi

if ! command -v codesign >/dev/null 2>&1; then
  echo "codesign is not available on this machine." >&2
  exit 1
fi

codesign --verify --deep --strict "${APP_PATH}"

runtime_result="not checked"
codesign_details="$(mktemp /tmp/tokenforge-codesign-details.XXXXXX)"
entitlements_details="$(mktemp /tmp/tokenforge-codesign-entitlements.XXXXXX)"
cleanup() {
  rm -f "${codesign_details}" "${entitlements_details}"
}
trap cleanup EXIT

codesign -dv "${APP_PATH}" >"${codesign_details}" 2>&1 || true
codesign -d --entitlements :- "${APP_PATH}" >"${entitlements_details}" 2>/dev/null || true

if grep -Eiq "runtime" "${codesign_details}"; then
  runtime_result="enabled"
elif [[ "${REQUIRE_RUNTIME}" == "true" ]]; then
  echo "Hardened runtime is required but was not detected." >&2
  exit 1
else
  runtime_result="not detected"
fi

spctl_result="skipped"
if command -v spctl >/dev/null 2>&1; then
  if spctl --assess --type execute --verbose "${APP_PATH}" >/dev/null 2>&1; then
    spctl_result="accepted"
  else
    spctl_result="not accepted"
    if [[ "${STRICT_SPCTL}" == "true" ]]; then
      echo "spctl assessment failed." >&2
      exit 1
    fi
  fi
fi

echo "TokenForge macOS signing verification summary"
echo "App path: ${APP_PATH}"
echo "Bundle identifier: ${EXPECTED_BUNDLE_ID}"
echo "codesign: verified"
echo "hardened runtime: ${runtime_result}"
echo "spctl: ${spctl_result}"
echo "Gatekeeper note: ad-hoc builds are expected not to pass full assessment; notarized Developer ID builds should pass."

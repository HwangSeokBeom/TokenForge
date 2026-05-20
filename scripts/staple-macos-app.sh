#!/usr/bin/env bash
set -euo pipefail

APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
STRICT_SPCTL="${TOKENFORGE_MACOS_STRICT_SPCTL:-true}"

if [[ ! -d "${APP_PATH}" ]]; then
  echo "TokenForge app bundle was not found. Set APP_PATH to a notarized .app bundle." >&2
  exit 1
fi

if ! command -v codesign >/dev/null 2>&1; then
  echo "codesign is not available on this machine." >&2
  exit 1
fi

if ! xcrun stapler help >/dev/null 2>&1; then
  echo "xcrun stapler is not available." >&2
  exit 1
fi

xcrun stapler staple "${APP_PATH}"
xcrun stapler validate "${APP_PATH}"
codesign --verify --deep --strict "${APP_PATH}"

spctl_result="skipped"
if command -v spctl >/dev/null 2>&1; then
  if spctl --assess --type execute --verbose "${APP_PATH}" >/dev/null 2>&1; then
    spctl_result="accepted"
  else
    spctl_result="not accepted"
    if [[ "${STRICT_SPCTL}" == "true" ]]; then
      echo "Gatekeeper assessment failed after stapling." >&2
      exit 1
    fi
  fi
fi

echo "TokenForge staple summary"
echo "App path: ${APP_PATH}"
echo "Staple: validated"
echo "codesign: verified"
echo "spctl: ${spctl_result}"
echo "Result: stapled and validated"

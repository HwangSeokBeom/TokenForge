#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

EXPECTED_VERSION="${TOKENFORGE_EXPECTED_VERSION:-0.18.0}"
EXPECTED_BUILD_NUMBER="${TOKENFORGE_EXPECTED_BUILD_NUMBER:-18}"
PACKAGE_PATH="${PACKAGE_PATH:-/tmp/tokenforge-release/TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip}"
RUN_PACKAGE="${TOKENFORGE_CLEAN_INSTALL_RUN_PACKAGE:-true}"
LAUNCH_APP="${TOKENFORGE_CLEAN_INSTALL_LAUNCH:-false}"

if [[ ! -f "${PACKAGE_PATH}" ]]; then
  if [[ "${RUN_PACKAGE}" == "true" ]]; then
    "${SCRIPT_DIR}/package-macos-release-candidate.sh"
  else
    echo "Release package was not found. Set PACKAGE_PATH or enable package creation." >&2
    exit 1
  fi
fi

VERIFY_DIR="$(mktemp -d /tmp/tokenforge-clean-install.XXXXXX)"
cleanup() {
  if [[ -n "${APP_PID:-}" ]]; then
    kill "${APP_PID}" >/dev/null 2>&1 || true
  fi
  rm -rf "${VERIFY_DIR}"
}
trap cleanup EXIT

unzip -q "${PACKAGE_PATH}" -d "${VERIFY_DIR}"
APP_PATH="${VERIFY_DIR}/TokenForge.app"
if [[ ! -d "${APP_PATH}" ]]; then
  echo "Clean install smoke failed: package did not unpack TokenForge.app." >&2
  exit 1
fi

if find "${APP_PATH}" -name 'tokenforge-approved-locations.local.json' -o -name 'tokenforge-save.json' -o -name '*.corrupt' -o -name '*.unsupported-schema' -o -name '*.unsafe' -o -name '.env' | grep -q .; then
  echo "Clean install smoke failed: package contains local data or recovery artifacts." >&2
  exit 1
fi

while IFS= read -r -d '' candidate; do
  if grep -Eiq 'bearer[[:space:]]+[a-z0-9._-]+|api[_-]?key[[:space:]]*[:=]|password[[:space:]]*[:=]|secret[[:space:]]*[:=]|raw[ _-]?sync[ _-]?payload' "${candidate}"; then
    echo "Clean install smoke failed: package contains sensitive marker text." >&2
    exit 1
  fi
done < <(find "${APP_PATH}" -type f \( -name '*.log' -o -name '*.json' -o -name '*.txt' -o -name '*.plist' \) -print0)

INFO_PLIST="${APP_PATH}/Contents/Info.plist"
if [[ ! -f "${INFO_PLIST}" ]]; then
  echo "Clean install smoke failed: Info.plist is missing." >&2
  exit 1
fi

executable_name="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "${INFO_PLIST}" 2>/dev/null || true)"
if [[ -z "${executable_name}" || ! -x "${APP_PATH}/Contents/MacOS/${executable_name}" ]]; then
  echo "Clean install smoke failed: executable is missing or not executable." >&2
  exit 1
fi

if [[ "${LAUNCH_APP}" == "true" ]]; then
  TOKENFORGE_SMOKE_DATA_DIR="${VERIFY_DIR}/first-run-data" open -n "${APP_PATH}"
  sleep 8
  app_pid="$(pgrep -f "${APP_PATH}/Contents/MacOS/${executable_name}" | head -n 1 || true)"
  if [[ -z "${app_pid}" ]]; then
    echo "Clean install smoke failed: app did not stay running after launch." >&2
    exit 1
  fi
  APP_PID="${app_pid}"
fi

echo "TokenForge clean install smoke summary"
echo "Package path: ${PACKAGE_PATH}"
echo "Install location: temporary"
echo "Structural checks: passed"
echo "Launch check: ${LAUNCH_APP}"
echo "Manual checklist:"
echo "- download/unzip release package."
echo "- launch app from a clean location and verify Gatekeeper behavior."
echo "- verify app launches without crash and Bootstrap scene appears."
echo "- verify Account panel appears."
echo "- verify Local Analysis panel appears."
echo "- verify Approved Local Locations panel appears."
echo "- verify Review panel appears."
echo "- verify Safe Sync panel appears."
echo "- verify Recent Sessions panel appears."
echo "- verify Privacy notice appears."
echo "- verify no login starts automatically."
echo "- verify no sync starts automatically."
echo "- verify no analysis starts automatically."
echo "- verify approved locations are empty on clean install."
echo "- verify local sessions are empty on clean install."
echo "- verify login is required only for server sync."
echo "- verify local analysis controls remain visible while logged out."
echo "- verify health check is explicit."
echo "- verify sync/fetch/delete require auth."
echo "- verify password input is masked."
echo "- verify no tokens/passwords/raw paths are visible."
echo "- verify app can quit/reopen."
echo "- verify no sensitive logs are emitted by TokenForge code."
echo "- verify no TokenForge logs contain credentials, tokens, raw sync payloads, raw local paths, prompts, responses, commands, or source snippets."
echo "Result: passed"

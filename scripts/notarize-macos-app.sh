#!/usr/bin/env bash
set -euo pipefail

APP_PATH="${APP_PATH:-/tmp/tokenforge-macos-build/TokenForge.app}"
PACKAGE_PATH="${PACKAGE_PATH:-/tmp/tokenforge-release/TokenForge.zip}"
NOTARIZE_ENABLED="${TOKENFORGE_RELEASE_NOTARIZE:-${TOKENFORGE_NOTARIZE:-false}}"
KEYCHAIN_PROFILE="${NOTARYTOOL_KEYCHAIN_PROFILE:-}"
ASYNC_MODE="${TOKENFORGE_NOTARIZE_ASYNC:-false}"
SAFE_SUMMARY_PATH="${TOKENFORGE_NOTARY_SAFE_SUMMARY_PATH:-}"

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  echo "Usage: TOKENFORGE_RELEASE_NOTARIZE=true PACKAGE_PATH=/path/TokenForge.zip scripts/notarize-macos-app.sh"
  echo "Uses a stored notary profile when present, otherwise direct Apple notary credentials."
  exit 0
fi

if [[ "${NOTARIZE_ENABLED}" != "true" ]]; then
  echo "TokenForge notarization is opt-in. Set TOKENFORGE_RELEASE_NOTARIZE=true to submit a package."
  exit 0
fi

if [[ ! -f "${PACKAGE_PATH}" ]]; then
  if [[ -d "${APP_PATH}" ]]; then
    mkdir -p "$(dirname "${PACKAGE_PATH}")"
    ditto -c -k --sequesterRsrc --keepParent "${APP_PATH}" "${PACKAGE_PATH}"
  else
    echo "Notarization package was not found. Set PACKAGE_PATH to a zip or dmg." >&2
    exit 1
  fi
fi

if ! xcrun notarytool --help >/dev/null 2>&1; then
  echo "xcrun notarytool is not available." >&2
  exit 1
fi

# The call below intentionally goes through xcrun notarytool submit only after opt-in validation.
submit_args=(submit "${PACKAGE_PATH}" --output-format json)
if [[ "${ASYNC_MODE}" != "true" ]]; then
  submit_args+=(--wait)
fi

notary_output="$(mktemp /tmp/tokenforge-notary-submit.XXXXXX.json)"
notary_error="$(mktemp /tmp/tokenforge-notary-submit.XXXXXX.err)"
cleanup() {
  rm -f "${notary_output}" "${notary_error}"
}
trap cleanup EXIT

credential_mode=""
notary_status="Failed"
notary_command_status=0
if [[ -n "${KEYCHAIN_PROFILE}" ]]; then
  credential_mode="keychain-profile"
  xcrun notarytool "${submit_args[@]}" --keychain-profile "${KEYCHAIN_PROFILE}" >"${notary_output}" 2>"${notary_error}" || notary_command_status=$?
elif [[ -n "${APPLE_ID:-}" && -n "${TEAM_ID:-}" && -n "${APP_SPECIFIC_PASSWORD:-}" ]]; then
  credential_mode="apple-id"
  xcrun notarytool "${submit_args[@]}" --apple-id "${APPLE_ID}" --team-id "${TEAM_ID}" --password "${APP_SPECIFIC_PASSWORD}" >"${notary_output}" 2>"${notary_error}" || notary_command_status=$?
else
  echo "Notarization requires configured Apple notary credentials." >&2
  exit 1
fi

extract_json_value() {
  local key="$1"
  /usr/bin/plutil -extract "${key}" raw -o - "${notary_output}" 2>/dev/null || true
}

parsed_status="$(extract_json_value status)"
if [[ -n "${parsed_status}" ]]; then
  notary_status="${parsed_status}"
elif [[ "${ASYNC_MODE}" == "true" && "${notary_command_status}" -eq 0 ]]; then
  notary_status="Submitted"
elif [[ "${notary_command_status}" -ne 0 ]]; then
  notary_status="Failed"
fi

if grep -Eiq 'timed[ -]?out|timeout' "${notary_output}" "${notary_error}" 2>/dev/null; then
  notary_status="Timed out"
fi

case "${notary_status}" in
  Accepted|Submitted|Invalid|Failed|"Timed out")
    ;;
  "In Progress")
    notary_status="Submitted"
    ;;
  *)
    notary_status="Failed"
    ;;
esac

if [[ -n "${SAFE_SUMMARY_PATH}" ]]; then
  {
    echo "TokenForge notarization safe diagnostic summary"
    echo "Package path: ${PACKAGE_PATH}"
    echo "Credential mode: ${credential_mode}"
    echo "Mode: $(if [[ "${ASYNC_MODE}" == "true" ]]; then echo "async"; else echo "wait"; fi)"
    echo "Status: ${notary_status}"
  } >"${SAFE_SUMMARY_PATH}"
fi

echo "TokenForge notarization summary"
echo "Package path: ${PACKAGE_PATH}"
echo "Credential mode: ${credential_mode}"
echo "Mode: $(if [[ "${ASYNC_MODE}" == "true" ]]; then echo "async"; else echo "wait"; fi)"
echo "Status: ${notary_status}"

if [[ "${notary_command_status}" -ne 0 ]]; then
  echo "Result: failed"
  exit "${notary_command_status}"
fi

if [[ "${ASYNC_MODE}" != "true" && "${notary_status}" != "Accepted" ]]; then
  echo "Result: ${notary_status}"
  exit 1
fi

echo "Result: submitted"

#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/tokenforge-unity-env.sh"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${REPO_ROOT}/UnityClient"

TEST_RESULTS="${TEST_RESULTS:-/tmp/tokenforge-live-auth-sync-results.xml}"
LOG_FILE="${LOG_FILE:-/tmp/tokenforge-live-auth-sync-unity.log}"

echo "TokenForge live auth/sync integration tests are opt-in and may create/delete test remote safe sessions."

required_vars=(
  TOKENFORGE_LIVE_INTEGRATION_ENABLED
  TOKENFORGE_LIVE_BASE_URL
  TOKENFORGE_LIVE_EMAIL
  TOKENFORGE_LIVE_PASSWORD
)

for var_name in "${required_vars[@]}"; do
  if [[ -z "${!var_name:-}" ]]; then
    echo "Missing required environment variable: ${var_name}" >&2
    exit 1
  fi
done

if [[ "${TOKENFORGE_LIVE_INTEGRATION_ENABLED}" != "true" ]]; then
  echo "TOKENFORGE_LIVE_INTEGRATION_ENABLED must be set to true." >&2
  exit 1
fi

if [[ ! "${TOKENFORGE_LIVE_BASE_URL}" =~ ^https?:// ]]; then
  echo "TOKENFORGE_LIVE_BASE_URL must start with http:// or https://." >&2
  exit 1
fi

if [[ "${TOKENFORGE_LIVE_ALLOW_SIGNUP:-false}" == "true" ]]; then
  if [[ -z "${TOKENFORGE_LIVE_SIGNUP_EMAIL:-}" || -z "${TOKENFORGE_LIVE_SIGNUP_PASSWORD:-}" ]]; then
    echo "Signup test requires TOKENFORGE_LIVE_SIGNUP_EMAIL and TOKENFORGE_LIVE_SIGNUP_PASSWORD." >&2
    exit 1
  fi
fi

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable: ${UNITY_PATH}" >&2
  echo "Set UNITY_PATH to a Unity 6000.4.10f1 executable." >&2
  exit 1
fi

echo "Base URL: ${TOKENFORGE_LIVE_BASE_URL}"
echo "Email: ${TOKENFORGE_LIVE_EMAIL%%@*}@<redacted-domain>"
echo "Passwords and tokens will not be echoed."

"${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -runTests \
  -testPlatform editmode \
  -testFilter "TokenForge.Client.Tests.Phase12LiveAuthSyncIntegrationTests" \
  -testResults "${TEST_RESULTS}" \
  -logFile "${LOG_FILE}"

echo "Live auth/sync test results: ${TEST_RESULTS}"
echo "Unity log: ${LOG_FILE}"

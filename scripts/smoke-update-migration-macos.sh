#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${REPO_ROOT}/UnityClient"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity}"
RESULTS_PATH="${RESULTS_PATH:-/tmp/tokenforge-phase19-migration-smoke-results.xml}"
LOG_FILE="${LOG_FILE:-/tmp/tokenforge-phase19-migration-smoke.log}"

if [[ ! -x "${UNITY_PATH}" ]]; then
  echo "Unity executable not found or not executable. Set UNITY_PATH to Unity 2022.3.0f1." >&2
  exit 1
fi

"${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${UNITY_PROJECT_PATH}" \
  -runTests \
  -testPlatform editmode \
  -testFilter "TokenForge.Client.Tests.Phase19MigrationSmokeTests" \
  -testResults "${RESULTS_PATH}" \
  -logFile "${LOG_FILE}"

echo "TokenForge update migration smoke summary"
echo "Fixture source: Tests/Fixtures/Persistence"
echo "Results: ${RESULTS_PATH}"
echo "Log path: ${LOG_FILE}"
echo "Manual packaged-build migration checklist:"
echo "- seed legacy v0/missing-schema safe save data in an isolated data directory."
echo "- seed legacy approved-location settings in the isolated data directory."
echo "- launch the new packaged app against the isolated data directory when a runtime data-dir override is available."
echo "- verify migration to schemaVersion 1."
echo "- verify corrupt files do not crash app."
echo "- verify unknown future schema fails safely."
echo "- verify approved-location paths remain local-only."
echo "- verify migrated safe save data remains aggregate-only."
echo "- verify no sync starts automatically."
echo "- verify backup/recovery files are local-only and not packaged."
echo "- verify app can quit/reopen after migration."
echo "- verify package does not include seeded local data."
echo "- verify logs do not expose raw approved-location paths or full save payloads."
echo "Result: passed"

#!/usr/bin/env bash
set -u -o pipefail

# Codex one-pass verifier for TokenForge.
#
# Unity Test Framework 1.1.33 can exit before result XML is flushed when
# `-quit` is combined with `-runTests`, so this script never passes `-quit`
# to Unity test invocations. Instead, every Unity test command is wrapped by
# an external Perl alarm timeout. Unity licensing failures are environmental;
# when they appear, this script stops with a BLOCKED report before any source
# edits are considered.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}" || exit 1

UNITY="/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$PWD/UnityClient"
ARTIFACT_DIR="$PROJECT/TestResults/codex-onepass-$(date +%Y%m%d-%H%M%S)"
BUILT_APP="/tmp/tokenforge-macos-build/TokenForge.app"
SOURCE_DLL="$PROJECT/Library/ScriptAssemblies/TokenForge.Client.dll"
SOURCE_DYLIB="$PROJECT/Assets/Plugins/macOS/libDesktopCompanionOverlay.dylib"

mkdir -p "$ARTIFACT_DIR"

STATUS="BLOCKED"
BUILD_RESULT="not_run"
HASH_SUMMARY="not_run"
RUNTIME_RESULT="not_run"
SYMBOL_RESULT="not_run"
DIFF_CHECK_RESULT="not_run"
SUMMARY_PATH="$ARTIFACT_DIR/summary.txt"
QUIT_USED="NO"
EXTERNAL_TIMEOUT_USED="YES"
SMOKE_ATTEMPTS=0
SMOKE_LAST_NAME=""
SMOKE_LAST_LOG=""
SMOKE_LAST_XML=""
SMOKE_INTERACTIVE_RETRY_USED="NO"
SMOKE_FINAL_XML_EXISTS="NO"
RESET_MODE="NO"
if [[ "${TOKENFORGE_FORCE_LICENSE_RESET:-0}" == "1" ]]; then
  RESET_MODE="YES"
fi
declare -a TEST_TOTALS
declare -a SMOKE_HISTORY

section() {
  printf '\n== %s ==\n' "$1"
}

print_changed_files() {
  git status --short
}

print_environment_summary() {
  section "Environment"
  echo "Repo path: $REPO_ROOT"
  if [[ -x "$UNITY" ]]; then
    echo "Unity path: exists executable ($UNITY)"
  elif [[ -e "$UNITY" ]]; then
    echo "Unity path: exists but not executable ($UNITY)"
  else
    echo "Unity path: missing ($UNITY)"
  fi
  echo "Git branch: $(git branch --show-current 2>/dev/null || echo unknown)"
  echo "Git status:"
  git status --short --branch
}

print_final_report() {
  local manual_checks="${1:-none}"
  section "Final report"
  echo "Status: $STATUS"
  echo "Summary file: $SUMMARY_PATH"
  echo "Changed files:"
  print_changed_files | sed 's/^/  /'
  echo "Script verification:"
  echo "  Unity path: $UNITY"
  echo "  Project path: $PROJECT"
  echo "  -quit used: $QUIT_USED"
  echo "  External timeout used: $EXTERNAL_TIMEOUT_USED"
  echo "  Licensing smoke attempts: $SMOKE_ATTEMPTS"
  echo "  Licensing smoke XML exists: $SMOKE_FINAL_XML_EXISTS"
  echo "Test totals:"
  if [[ ${#TEST_TOTALS[@]} -eq 0 ]]; then
    echo "  not_run"
  else
    printf '  %s\n' "${TEST_TOTALS[@]}"
  fi
  echo "Diff check result: $DIFF_CHECK_RESULT"
  echo "Native symbol result: $SYMBOL_RESULT"
  echo "Build result: $BUILD_RESULT"
  echo "Hash summary: $HASH_SUMMARY"
  echo "Runtime result: $RUNTIME_RESULT"
  echo "Artifact directory: $ARTIFACT_DIR"
  echo "Remaining manual checks: $manual_checks"
  if [[ "$STATUS" == "LICENSING_BLOCKED" ]]; then
    echo "Instruction: run with TOKENFORGE_FORCE_LICENSE_RESET=1 from normal macOS Terminal after confirming Unity Hub login."
  elif [[ "$STATUS" == "PASS" ]]; then
    echo "Instruction: PASS includes built app hash summary and runtime smoke result above."
  fi
}

write_summary() {
  local manual_checks="${1:-none}"
  {
    echo "final status: $STATUS"
    echo "artifact dir: $ARTIFACT_DIR"
    echo "Unity path: $UNITY"
    echo "project path: $PROJECT"
    echo "-quit used: $QUIT_USED"
    echo "external timeout used: $EXTERNAL_TIMEOUT_USED"
    echo "reset mode: $RESET_MODE"
    echo "smoke attempts: $SMOKE_ATTEMPTS"
    echo "smoke XML existence: $SMOKE_FINAL_XML_EXISTS"
    if [[ ${#SMOKE_HISTORY[@]} -eq 0 ]]; then
      echo "smoke history: not_run"
    else
      echo "smoke history:"
      printf '  %s\n' "${SMOKE_HISTORY[@]}"
    fi
    echo "test status:"
    if [[ ${#TEST_TOTALS[@]} -eq 0 ]]; then
      echo "  not_run"
    else
      printf '  %s\n' "${TEST_TOTALS[@]}"
    fi
    echo "touched-file git diff --check status: $DIFF_CHECK_RESULT"
    echo "native symbol status: $SYMBOL_RESULT"
    echo "build status: $BUILD_RESULT"
    echo "hash status: $HASH_SUMMARY"
    echo "runtime status: $RUNTIME_RESULT"
    echo "manual checks: $manual_checks"
    if [[ "$STATUS" == "LICENSING_BLOCKED" && -n "$SMOKE_LAST_LOG" ]]; then
      echo "exact licensing lines:"
      grep -Eai "licen[cs].*(error|fail|timed|invalid|missing|abort|denied)|Licensing::IpcConnector.*failed|IPC channel to LicensingClient doesn't exist|Timed-out after .*waiting for channel|No valid Unity Editor license|ULS|Entitlement|Machine is not activated|This should not be called in batch mode" "$SMOKE_LAST_LOG" 2>/dev/null | tail -80 || true
    else
      echo "exact licensing lines: none"
    fi
    if [[ "$STATUS" == "LICENSING_BLOCKED" ]]; then
      echo "Instruction: run with TOKENFORGE_FORCE_LICENSE_RESET=1 from normal macOS Terminal after confirming Unity Hub login."
    elif [[ "$STATUS" == "PASS" ]]; then
      echo "Instruction: PASS includes built app hash summary and runtime smoke result."
    fi
  } >"$SUMMARY_PATH"
}

finish() {
  local exit_code="$1"
  local manual_checks="${2:-none}"
  write_summary "$manual_checks"
  print_final_report "$manual_checks"
  exit "$exit_code"
}

clean_stale_lock() {
  local lock_file="$PROJECT/Temp/UnityLockfile"
  section "Unity lock cleanup"
  if [[ ! -f "$lock_file" ]]; then
    echo "No Unity lockfile present: $lock_file"
    return 0
  fi

  echo "Unity lockfile present: $lock_file"
  if ! command -v lsof >/dev/null 2>&1; then
    echo "WARNING: lsof unavailable; not deleting lockfile blindly."
    return 0
  fi

  local lsof_output
  lsof_output="$ARTIFACT_DIR/unity-lock-lsof.txt"
  if lsof "$lock_file" >"$lsof_output" 2>&1; then
    echo "Lockfile has active holder(s); leaving it in place."
    cat "$lsof_output"
    return 0
  fi

  if grep -qi "permission denied" "$lsof_output"; then
    echo "WARNING: lsof permission denied; not deleting lockfile blindly."
    cat "$lsof_output"
    return 0
  fi

  if [[ -s "$lsof_output" ]]; then
    echo "WARNING: lsof could not prove the lock is stale; not deleting lockfile blindly."
    cat "$lsof_output"
    return 0
  fi

  rm -f "$lock_file"
  echo "Removed stale Unity lockfile: $lock_file"
}

collect_safe_process_pids() {
  local ps_output ps_status
  ps_output="$(ps axww -o pid= -o command= 2>&1)"
  ps_status=$?
  if [[ $ps_status -ne 0 ]]; then
    echo "WARNING: process listing unavailable; safe stale process cleanup could not inspect processes." >&2
    echo "$ps_output" >&2
    return 3
  fi

  printf '%s\n' "$ps_output" | while IFS= read -r line; do
    local pid cmd
    pid="${line%% *}"
    cmd="${line#* }"
    [[ -n "$pid" && "$pid" =~ ^[0-9]+$ ]] || continue
    case "$cmd" in
      *"Unity Hub"*)
        continue
        ;;
    esac
    if [[ "$cmd" == *"VBCSCompiler.dll"* ]]; then
      echo "$pid"
      continue
    fi
    if [[ "$cmd" == *"UnityAutoQuitter"* ]]; then
      echo "$pid"
      continue
    fi
    if [[ "$cmd" == *"$PROJECT"* && "$cmd" == *"-batchmode"* && "$cmd" == *"Unity"* ]]; then
      echo "$pid"
      continue
    fi
  done
}

kill_reset_mode_stale_processes() {
  section "Reset-mode stale process cleanup"
  local pids collect_status
  pids="$(collect_safe_process_pids | sort -u)"
  collect_status=$?
  if [[ $collect_status -ne 0 ]]; then
    echo "Skipped reset-mode stale process cleanup because process listing was unavailable."
    return 0
  fi

  if [[ -z "$pids" ]]; then
    echo "No stale Unity batchmode/UnityAutoQuitter/VBCSCompiler remnants found."
    return 0
  fi

  echo "Will terminate stale process(es):"
  while IFS= read -r pid; do
    [[ -n "$pid" ]] || continue
    ps -p "$pid" -o pid= -o command=
  done <<<"$pids"

  while IFS= read -r pid; do
    [[ -n "$pid" ]] || continue
    kill "$pid" 2>/dev/null || true
  done <<<"$pids"

  sleep 2
  while IFS= read -r pid; do
    [[ -n "$pid" ]] || continue
    if kill -0 "$pid" 2>/dev/null; then
      echo "Force-killing remaining stale process: $pid"
      kill -9 "$pid" 2>/dev/null || true
    fi
  done <<<"$pids"
}

detect_license_error() {
  local log_path="$1"
  [[ -f "$log_path" ]] || return 1
  grep -Eai "licen[cs].*(error|fail|timed|invalid|missing|abort|denied)|Licensing::IpcConnector.*failed|IPC channel to LicensingClient doesn't exist|Timed-out after .*waiting for channel|No valid Unity Editor license|ULS|Entitlement|Machine is not activated|This should not be called in batch mode" "$log_path" >/dev/null
}

print_license_error() {
  local log_path="$1"
  echo "Unity licensing error detected."
  echo "Log path: $log_path"
  grep -Eai "licen[cs].*(error|fail|timed|invalid|missing|abort|denied)|Licensing::IpcConnector.*failed|IPC channel to LicensingClient doesn't exist|Timed-out after .*waiting for channel|No valid Unity Editor license|ULS|Entitlement|Machine is not activated|This should not be called in batch mode" "$log_path" | tail -40 || true
}

force_license_reset_if_requested() {
  local reason="${1:-manual request}"
  if [[ "${TOKENFORGE_FORCE_LICENSE_RESET:-0}" != "1" ]]; then
    echo "TOKENFORGE_FORCE_LICENSE_RESET is not set; leaving Unity LicensingClient/Hub session running."
    return 0
  fi

  section "Forced Unity licensing reset"
  echo "TOKENFORGE_FORCE_LICENSE_RESET=1; resetting Unity licensing helper processes ($reason)."

  local ps_output ps_status pids
  ps_output="$(ps axww -o pid= -o command= 2>&1)"
  ps_status=$?
  if [[ $ps_status -ne 0 ]]; then
    echo "WARNING: process listing unavailable; could not inspect Unity licensing helpers."
    echo "$ps_output"
    return 0
  fi

  pids="$(
    printf '%s\n' "$ps_output" |
      awk '
        /Unity\.Licensing\.Client|Unity-LicenseClient|LicensingClient|LicenseClient-/ {
          if ($0 !~ /awk /) print $1
        }
      ' |
      sort -u
  )"

  if [[ -z "$pids" ]]; then
    echo "No Unity licensing helper process matched reset patterns."
    return 0
  fi

  echo "Terminating Unity licensing helper process(es):"
  while IFS= read -r pid; do
    [[ -n "$pid" ]] || continue
    ps -p "$pid" -o pid= -o command=
    kill "$pid" 2>/dev/null || true
  done <<<"$pids"

  sleep 2
  while IFS= read -r pid; do
    [[ -n "$pid" ]] || continue
    if kill -0 "$pid" 2>/dev/null; then
      echo "Force-killing remaining Unity licensing helper process: $pid"
      kill -9 "$pid" 2>/dev/null || true
    fi
  done <<<"$pids"
}

prepare_reset_mode_if_requested() {
  if [[ "${TOKENFORGE_FORCE_LICENSE_RESET:-0}" != "1" ]]; then
    section "Verification mode"
    echo "Default safe mode: not killing Unity Hub or Unity licensing helpers."
    return 0
  fi

  section "Verification mode"
  echo "Reset mode enabled by TOKENFORGE_FORCE_LICENSE_RESET=1."
  kill_reset_mode_stale_processes
  force_license_reset_if_requested "before licensing smoke"

  if [[ -t 0 && -t 1 ]]; then
    echo "Opening Unity Hub once. Confirm login/license is active, then press Enter to continue."
    /usr/bin/open -a "Unity Hub" >/dev/null 2>&1 || true
    IFS= read -r _
  else
    echo "Non-interactive terminal; not opening Unity Hub or waiting for confirmation."
  fi
}

xml_summary_line() {
  local xml_path="$1"
  /usr/bin/python3 - "$xml_path" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
passed = int(root.attrib.get("passed", "0"))
failed = int(root.attrib.get("failed", "0"))
inconclusive = int(root.attrib.get("inconclusive", "0"))
skipped = int(root.attrib.get("skipped", "0"))
total = int(root.attrib.get("total", passed + failed + inconclusive + skipped))
print(f"total={total} passed={passed} failed={failed} inconclusive={inconclusive} skipped={skipped}")
PY
}

xml_value() {
  local xml_path="$1"
  local key="$2"
  /usr/bin/python3 - "$xml_path" "$key" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
key = sys.argv[2]
if key == "total":
    passed = int(root.attrib.get("passed", "0"))
    failed = int(root.attrib.get("failed", "0"))
    inconclusive = int(root.attrib.get("inconclusive", "0"))
    skipped = int(root.attrib.get("skipped", "0"))
    print(root.attrib.get("total", str(passed + failed + inconclusive + skipped)))
else:
    print(root.attrib.get(key, "0"))
PY
}

print_failed_cases() {
  local xml_path="$1"
  /usr/bin/python3 - "$xml_path" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
failed_cases = [case for case in root.iter("test-case") if case.attrib.get("result") == "Failed"]
if not failed_cases:
    print("Failed cases: none")
    raise SystemExit(0)

print("Failed cases:")
for case in failed_cases:
    print(f"- {case.attrib.get('fullname') or case.attrib.get('name')}")
    failure = case.find("failure")
    if failure is not None:
        message = failure.findtext("message") or ""
        stack = failure.findtext("stack-trace") or ""
        if message.strip():
            print("  Message:")
            for line in message.strip().splitlines():
                print(f"    {line}")
        if stack.strip():
            print("  Stack trace:")
            for line in stack.strip().splitlines():
                print(f"    {line}")
PY
}

run_unity_test() {
  local name="$1"
  local filter="$2"
  local timeout_seconds="$3"
  local xml_path="$ARTIFACT_DIR/${name}.xml"
  local log_path="$ARTIFACT_DIR/${name}.log"

  section "Unity EditMode test: $name"
  rm -f "$xml_path" "$log_path"

  /usr/bin/perl -e 'alarm shift; exec @ARGV' "$timeout_seconds" \
    "$UNITY" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT" \
    -runTests \
    -testPlatform editmode \
    -testFilter "$filter" \
    -testResults "$xml_path" \
    -logFile "$log_path"
  local unity_exit=$?

  echo "Unity exit code: $unity_exit"
  echo "Log path: $log_path"
  echo "XML path: $xml_path"

  if [[ -s "$xml_path" ]]; then
    echo "XML_EXISTS=YES"
    local summary
    summary="$(xml_summary_line "$xml_path")"
    echo "Test run: $summary"
    TEST_TOTALS+=("$name: $summary")
    print_failed_cases "$xml_path"
  else
    echo "XML_EXISTS=NO"
  fi

  if [[ $unity_exit -ne 0 ]]; then
    if detect_license_error "$log_path"; then
      STATUS="TESTS_FAILED"
      print_license_error "$log_path"
      return "$unity_exit"
    fi
  fi

  return "$unity_exit"
}

require_smoke_success() {
  local name="$1"
  local xml_path="$ARTIFACT_DIR/${name}.xml"
  local log_path="$ARTIFACT_DIR/${name}.log"

  if [[ ! -s "$xml_path" ]]; then
    STATUS="BLOCKED"
    echo "Smoke test did not produce result XML."
    if detect_license_error "$log_path"; then
      print_license_error "$log_path"
    fi
    echo "Last 160 log lines:"
    tail -160 "$log_path" 2>/dev/null || true
    finish 2 "Resolve Unity test runner/licensing blocker, then rerun the onepass script"
  fi

  local total passed failed
  total="$(xml_value "$xml_path" total)"
  passed="$(xml_value "$xml_path" passed)"
  failed="$(xml_value "$xml_path" failed)"
  if [[ "$total" != "1" || "$passed" != "1" || "$failed" != "0" ]]; then
    STATUS="BLOCKED"
    echo "Smoke test result mismatch: expected total=1 passed=1 failed=0"
    echo "Actual: $(xml_summary_line "$xml_path")"
    echo "Last 160 log lines:"
    tail -160 "$log_path" 2>/dev/null || true
    finish 2 "Fix Unity/NUnit discovery smoke before continuing"
  fi

  if ! grep -q "Running tests for editmode" "$log_path"; then
    STATUS="BLOCKED"
    echo "Smoke log is missing: Running tests for editmode"
    echo "Last 160 log lines:"
    tail -160 "$log_path" 2>/dev/null || true
    finish 2 "Fix Unity test runner logging/discovery before continuing"
  fi

  if ! grep -q "PHASE NUnit discovery smoke" "$log_path"; then
    STATUS="BLOCKED"
    echo "Smoke log is missing: PHASE NUnit discovery smoke"
    echo "Last 160 log lines:"
    tail -160 "$log_path" 2>/dev/null || true
    finish 2 "Fix NUnit discovery smoke before continuing"
  fi
}

smoke_xml_passed() {
  local xml_path="$1"
  [[ -s "$xml_path" ]] || return 1

  local total passed failed
  total="$(xml_value "$xml_path" total)"
  passed="$(xml_value "$xml_path" passed)"
  failed="$(xml_value "$xml_path" failed)"
  [[ "$total" == "1" && "$passed" == "1" && "$failed" == "0" ]]
}

run_licensing_smoke_attempt() {
  local name="$1"
  local timeout_seconds="$2"
  local xml_path="$ARTIFACT_DIR/${name}.xml"
  local log_path="$ARTIFACT_DIR/${name}.log"
  local xml_exists="NO"

  section "Unity licensing/NUnit discovery smoke: $name"
  rm -f "$xml_path" "$log_path"
  SMOKE_ATTEMPTS=$((SMOKE_ATTEMPTS + 1))
  SMOKE_LAST_NAME="$name"
  SMOKE_LAST_LOG="$log_path"
  SMOKE_LAST_XML="$xml_path"

  /usr/bin/perl -e 'alarm shift; exec @ARGV' "$timeout_seconds" \
    "$UNITY" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT" \
    -runTests \
    -testPlatform editmode \
    -testFilter "TokenForge.Client.Tests.NativeDashboardBridgeTests.NativeDashboardBridgeNUnitDiscoverySmoke" \
    -testResults "$xml_path" \
    -logFile "$log_path"
  local unity_exit=$?

  echo "Unity exit code: $unity_exit"
  echo "Log path: $log_path"
  echo "XML path: $xml_path"

  if [[ -s "$xml_path" ]]; then
    xml_exists="YES"
    SMOKE_FINAL_XML_EXISTS="YES"
    echo "XML_EXISTS=YES"
    local summary
    summary="$(xml_summary_line "$xml_path")"
    echo "Test run: $summary"
    TEST_TOTALS+=("$name: $summary")
    print_failed_cases "$xml_path"
  else
    SMOKE_FINAL_XML_EXISTS="NO"
    echo "XML_EXISTS=NO"
  fi

  SMOKE_HISTORY+=("$name: exit=$unity_exit xml_exists=$xml_exists")

  if smoke_xml_passed "$xml_path"; then
    return 0
  fi

  return 1
}

run_licensing_smoke_retry() {
  local attempts=3
  local attempt name log_path xml_path last_name=""
  local saw_license_error=0

  for attempt in $(seq 1 "$attempts"); do
    name="nunit-discovery-smoke-attempt-${attempt}"
    last_name="$name"
    log_path="$ARTIFACT_DIR/${name}.log"
    xml_path="$ARTIFACT_DIR/${name}.xml"

    if run_licensing_smoke_attempt "$name" 300; then
      echo "Licensing smoke passed on attempt $attempt."
      require_smoke_success "$name"
      return 0
    fi

    if detect_license_error "$log_path"; then
      saw_license_error=1
      print_license_error "$log_path"
    else
      echo "Licensing smoke attempt $attempt did not pass XML criteria."
      echo "Last 160 log lines:"
      tail -160 "$log_path" 2>/dev/null || true
    fi

    if [[ $attempt -lt $attempts ]]; then
      echo "Retrying licensing smoke in 5 seconds (attempt $((attempt + 1))/$attempts)."
      sleep 5
    fi
  done

  if [[ -t 0 && -t 1 ]]; then
    echo "Open Unity Hub, confirm login/license is active, then press Enter to retry."
    IFS= read -r _
    SMOKE_INTERACTIVE_RETRY_USED="YES"
    name="nunit-discovery-smoke-interactive-retry"
    last_name="$name"
    log_path="$ARTIFACT_DIR/${name}.log"
    xml_path="$ARTIFACT_DIR/${name}.xml"
    if run_licensing_smoke_attempt "$name" 300; then
      echo "Licensing smoke passed on interactive retry."
      require_smoke_success "$name"
      return 0
    fi
    if detect_license_error "$log_path"; then
      saw_license_error=1
      print_license_error "$log_path"
    else
      echo "Interactive licensing smoke retry did not pass XML criteria."
      echo "Last 160 log lines:"
      tail -160 "$log_path" 2>/dev/null || true
    fi
  fi

  if [[ ! -s "$ARTIFACT_DIR/${last_name}.xml" ]]; then
    STATUS="LICENSING_BLOCKED"
    echo "Licensing smoke did not produce result XML after $SMOKE_ATTEMPTS attempt(s)."
    if [[ $saw_license_error -eq 1 ]]; then
      print_license_error "$ARTIFACT_DIR/${last_name}.log"
    else
      echo "Last 160 log lines:"
      tail -160 "$ARTIFACT_DIR/${last_name}.log" 2>/dev/null || true
    fi
    finish 2 "Unity LicensingClient IPC/channel or Unity test runner did not produce smoke XML"
  fi

  if [[ $saw_license_error -eq 1 ]]; then
    STATUS="LICENSING_BLOCKED"
    echo "All licensing smoke retries failed."
    print_license_error "$ARTIFACT_DIR/${last_name}.log"
    finish 2 "Unity LicensingClient IPC/channel did not become ready after $attempts smoke attempts"
  fi

  STATUS="BLOCKED"
  echo "All NUnit discovery smoke retries failed without a recognized licensing error."
  if [[ -n "$last_name" ]]; then
    echo "Last XML path: $ARTIFACT_DIR/${last_name}.xml"
    echo "Last log path: $ARTIFACT_DIR/${last_name}.log"
  fi
  finish 2 "Fix Unity/NUnit discovery smoke before continuing"
}

run_core_test_or_stop() {
  local name="$1"
  local filter="$2"
  if ! run_unity_test "$name" "$filter" 900; then
    local log_path="$ARTIFACT_DIR/${name}.log"
    if detect_license_error "$log_path"; then
      STATUS="TESTS_FAILED"
      print_license_error "$log_path"
      echo "Test command failed after licensing smoke had passed."
      echo "Last 160 log lines:"
      tail -160 "$log_path" 2>/dev/null || true
      finish 1 "Inspect Unity test log and rerun onepass"
    fi
  fi

  local xml_path="$ARTIFACT_DIR/${name}.xml"
  if [[ ! -s "$xml_path" ]]; then
    STATUS="TESTS_FAILED"
    echo "Core test did not produce XML: $xml_path"
    tail -160 "$ARTIFACT_DIR/${name}.log" 2>/dev/null || true
    finish 1 "Inspect Unity test log and rerun onepass"
  fi

  local failed
  failed="$(xml_value "$xml_path" failed)"
  if [[ "$failed" != "0" ]]; then
    STATUS="TESTS_FAILED"
    echo "Core test failed: $name"
    echo "XML path: $xml_path"
    echo "Log path: $ARTIFACT_DIR/${name}.log"
    print_failed_cases "$xml_path"
    finish 1 "Fix failing root cause, then rerun onepass"
  fi
}

run_native_symbol_verification() {
  section "Native symbol verification"
  if ./scripts/verify-native-symbols.sh; then
    SYMBOL_RESULT="success"
    echo "Native symbol verification: success"
  else
    SYMBOL_RESULT="failure"
    STATUS="TESTS_FAILED"
    echo "Native symbol verification failed."
    finish 1 "Fix native symbol mismatch, then rerun onepass"
  fi
}

run_touched_file_diff_check() {
  section "Touched-file git diff --check"
  local diff_log="$ARTIFACT_DIR/git-diff-check.log"
  if git diff --check -- . ':!UnityClient/TestResults' >"$diff_log" 2>&1; then
    DIFF_CHECK_RESULT="success log=$diff_log"
    echo "git diff --check: success"
  else
    DIFF_CHECK_RESULT="failure log=$diff_log"
    STATUS="TESTS_FAILED"
    echo "git diff --check failed."
    cat "$diff_log"
    finish 1 "Fix whitespace/conflict-marker issues, then rerun onepass"
  fi
}

run_macos_smoke_build() {
  section "macOS smoke build"
  local build_log="$ARTIFACT_DIR/build-macos-smoke.log"
  scripts/build-macos-smoke.sh 2>&1 | tee "$build_log"
  local build_exit=${PIPESTATUS[0]}
  if [[ $build_exit -eq 0 ]]; then
    BUILD_RESULT="success log=$build_log"
  else
    BUILD_RESULT="failure exit=$build_exit log=$build_log"
    if detect_license_error "$build_log"; then
      STATUS="BUILD_FAILED"
      print_license_error "$build_log"
      finish 1 "Inspect macOS smoke build licensing/build log, then rerun onepass"
    fi
    STATUS="BUILD_FAILED"
    finish 1 "Fix macOS smoke build failure, then rerun onepass"
  fi

  if [[ ! -d "$BUILT_APP" ]]; then
    BUILD_RESULT="failure missing_app=$BUILT_APP log=$build_log"
    STATUS="BUILD_FAILED"
    echo "Built app missing: $BUILT_APP"
    finish 1 "Fix macOS build output, then rerun onepass"
  fi
}

sha256_file() {
  local path="$1"
  shasum -a 256 "$path" | awk '{print $1}'
}

timestamp_file() {
  local path="$1"
  stat -f "%Sm" -t "%Y-%m-%d %H:%M:%S %z" "$path"
}

print_hash_entry() {
  local label="$1"
  local path="$2"
  if [[ -f "$path" ]]; then
    echo "$label path: $path"
    echo "$label timestamp: $(timestamp_file "$path")"
    echo "$label sha256: $(sha256_file "$path")"
  else
    echo "$label path: missing ($path)"
  fi
}

first_found_in_app() {
  local name="$1"
  find "$BUILT_APP" -type f -name "$name" -print 2>/dev/null | sort | head -1
}

verify_fresh_artifacts() {
  section "Fresh artifact hashes"
  local bundled_dll bundled_dylib
  bundled_dll="$(first_found_in_app "TokenForge.Client.dll")"
  bundled_dylib="$(first_found_in_app "libDesktopCompanionOverlay.dylib")"

  print_hash_entry "Bundled TokenForge.Client.dll" "$bundled_dll"
  print_hash_entry "Bundled libDesktopCompanionOverlay.dylib" "$bundled_dylib"
  print_hash_entry "Source Library/ScriptAssemblies/TokenForge.Client.dll" "$SOURCE_DLL"
  print_hash_entry "Source Assets/Plugins/macOS/libDesktopCompanionOverlay.dylib" "$SOURCE_DYLIB"

  if [[ ! -f "$bundled_dll" || ! -f "$SOURCE_DLL" ]]; then
    STATUS="BLOCKED"
    HASH_SUMMARY="BLOCKED/STALE_BUILD missing bundled_or_source_dll"
    finish 2 "Ensure Unity compile output and bundled DLL exist, then rerun onepass"
  fi

  if [[ "$(sha256_file "$bundled_dll")" != "$(sha256_file "$SOURCE_DLL")" ]]; then
    STATUS="BLOCKED"
    HASH_SUMMARY="BLOCKED/STALE_BUILD bundled_dll_hash_mismatch"
    finish 2 "Rebuild until bundled DLL matches current compile output"
  fi

  if [[ ! -f "$bundled_dylib" || ! -f "$SOURCE_DYLIB" ]]; then
    STATUS="BLOCKED"
    HASH_SUMMARY="BLOCKED/STALE_NATIVE_BUNDLE missing bundled_or_source_dylib"
    finish 2 "Ensure native plugin source and bundled dylib exist, then rerun onepass"
  fi

  if [[ "$(sha256_file "$bundled_dylib")" != "$(sha256_file "$SOURCE_DYLIB")" ]]; then
    STATUS="BLOCKED"
    HASH_SUMMARY="BLOCKED/STALE_NATIVE_BUNDLE bundled_dylib_hash_mismatch"
    finish 2 "Rebuild until bundled dylib matches source plugin dylib"
  fi

  HASH_SUMMARY="fresh bundled DLL and dylib match source artifacts"
}

runtime_smoke() {
  section "Optional runtime smoke"
  local smoke_log="$ARTIFACT_DIR/runtime-smoke.log"
  local app_log_dir="$HOME/Library/Logs/DefaultCompany/TokenForge"
  local before_pids after_pids launched_pid

  before_pids="$(pgrep -fl "TokenForge.app|/TokenForge$" 2>/dev/null || true)"
  {
    echo "Built app: $BUILT_APP"
    echo "Existing TokenForge processes before launch:"
    echo "$before_pids"
  } >"$smoke_log"

  if /usr/bin/open -n "$BUILT_APP" >>"$smoke_log" 2>&1; then
    sleep 5
    after_pids="$(pgrep -fl "TokenForge.app|/TokenForge$" 2>/dev/null || true)"
    launched_pid="$(comm -13 <(printf '%s\n' "$before_pids" | awk '{print $1}' | sort) <(printf '%s\n' "$after_pids" | awk '{print $1}' | sort) | head -1)"
    {
      echo "TokenForge processes after launch:"
      echo "$after_pids"
      echo "Detected launched pid: ${launched_pid:-none}"
      if [[ -d "$app_log_dir" ]]; then
        echo "Recent app logs:"
        find "$app_log_dir" -type f -print 2>/dev/null | while IFS= read -r app_log; do
          echo "-- $app_log"
          tail -40 "$app_log" 2>/dev/null || true
        done
      else
        echo "App log directory not found: $app_log_dir"
      fi
    } >>"$smoke_log"

    if [[ -n "$launched_pid" ]]; then
      RUNTIME_RESULT="verified process_start pid=$launched_pid log=$smoke_log"
      return 0
    fi

    RUNTIME_RESULT="launch_command_succeeded_but_process_not_verified log=$smoke_log"
    STATUS="RUNTIME_FAILED"
    return 1
  fi

  RUNTIME_RESULT="launch_command_failed log=$smoke_log"
  STATUS="RUNTIME_FAILED"
  return 1
}

main() {
  print_environment_summary

  if [[ ! -x "$UNITY" ]]; then
    STATUS="BLOCKED"
    finish 2 "Install or expose Unity at $UNITY"
  fi

  clean_stale_lock
  prepare_reset_mode_if_requested

  run_licensing_smoke_retry

  run_core_test_or_stop "native-dashboard-bridge" "TokenForge.Client.Tests.NativeDashboardBridgeTests"
  run_core_test_or_stop "repository-companion-profile" "TokenForge.Client.Tests.RepositoryCompanionProfileTests"
  run_core_test_or_stop "token-shop" "TokenForge.Client.Tests.*TokenShop*"
  run_core_test_or_stop "wardrobe" "TokenForge.Client.Tests.*Wardrobe*"
  run_core_test_or_stop "onboarding" "TokenForge.Client.Tests.*Onboarding*"

  run_touched_file_diff_check
  run_native_symbol_verification
  run_macos_smoke_build
  verify_fresh_artifacts

  if runtime_smoke; then
    STATUS="PASS"
    finish 0 "none"
  fi

  finish 1 "Runtime launch could not be verified automatically; inspect $ARTIFACT_DIR/runtime-smoke.log"
}

main "$@"

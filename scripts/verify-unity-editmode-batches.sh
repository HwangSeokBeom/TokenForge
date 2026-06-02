#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
TEST_DIR="${TEST_DIR:-${REPO_ROOT}/UnityClient/Assets/_Project/Tests/EditMode}"
RESULT_DIR="${RESULT_DIR:-/tmp/tokenforge-editmode-batches}"
UNITY_TEST_TIMEOUT_SECONDS="${UNITY_TEST_TIMEOUT_SECONDS:-180}"
FIXTURE_LIST="${FIXTURE_LIST:-}"
STOP_ON_FAIL="${STOP_ON_FAIL:-false}"
BATCH_SIZE="${BATCH_SIZE:-1}"

if ! [[ "${UNITY_TEST_TIMEOUT_SECONDS}" =~ ^[0-9]+$ ]] || [[ "${UNITY_TEST_TIMEOUT_SECONDS}" -le 0 ]]; then
  echo "UNITY_TEST_TIMEOUT_SECONDS must be a positive integer, got: ${UNITY_TEST_TIMEOUT_SECONDS}" >&2
  exit 1
fi

if ! [[ "${BATCH_SIZE}" =~ ^[0-9]+$ ]] || [[ "${BATCH_SIZE}" -le 0 ]]; then
  echo "BATCH_SIZE must be a positive integer, got: ${BATCH_SIZE}" >&2
  exit 1
fi

sanitize_name() {
  printf '%s' "$1" | tr '.+/' '___' | tr -cd 'A-Za-z0-9_-.'
}

discover_fixtures() {
  find "${TEST_DIR}" -maxdepth 1 -name '*.cs' -print0 | sort -z | while IFS= read -r -d '' file; do
    if ! grep -Eq '\[(Test|UnityTest|TestFixture)\]' "${file}"; then
      continue
    fi

    local namespace
    namespace="$(sed -nE 's/^[[:space:]]*namespace[[:space:]]+([A-Za-z0-9_.]+).*/\1/p' "${file}" | head -n 1)"
    if [[ -z "${namespace}" ]]; then
      continue
    fi

    sed -nE 's/^[[:space:]]*public[[:space:]]+(sealed[[:space:]]+)?class[[:space:]]+([A-Za-z_][A-Za-z0-9_]*).*/\2/p' "${file}" |
      while IFS= read -r class_name; do
        printf '%s.%s\n' "${namespace}" "${class_name}"
      done
  done
}

if [[ -n "${FIXTURE_LIST}" ]]; then
  fixtures=()
  while IFS= read -r fixture; do
    fixtures+=("${fixture}")
  done < "${FIXTURE_LIST}"
else
  fixtures=()
  while IFS= read -r fixture; do
    fixtures+=("${fixture}")
  done < <(discover_fixtures)
fi

if [[ "${#fixtures[@]}" -eq 0 ]]; then
  echo "No EditMode fixtures discovered under ${TEST_DIR}" >&2
  exit 1
fi

mkdir -p "${RESULT_DIR}"

echo "EditMode batch fixture count: ${#fixtures[@]}"
echo "EditMode batch size: ${BATCH_SIZE}"
echo "EditMode batch timeout seconds: ${UNITY_TEST_TIMEOUT_SECONDS}"
echo "EditMode batch result dir: ${RESULT_DIR}"

overall=0
batch_index=1
batch_fixtures=()
for fixture in "${fixtures[@]}" "__FLUSH__"; do
  if [[ "${fixture}" != "__FLUSH__" && ( -z "${fixture}" || "${fixture}" =~ ^[[:space:]]*# ) ]]; then
    continue
  fi

  if [[ "${fixture}" != "__FLUSH__" ]]; then
    batch_fixtures+=("${fixture}")
  fi

  if [[ "${fixture}" != "__FLUSH__" && "${#batch_fixtures[@]}" -lt "${BATCH_SIZE}" ]]; then
    continue
  fi

  if [[ "${#batch_fixtures[@]}" -eq 0 ]]; then
    continue
  fi

  filter="$(IFS=';'; printf '%s' "${batch_fixtures[*]}")"
  if [[ "${BATCH_SIZE}" -eq 1 ]]; then
    label="${batch_fixtures[0]}"
  else
    label="$(printf 'batch_%03d' "${batch_index}")"
  fi
  safe_name="$(sanitize_name "${label}")"
  xml_path="${RESULT_DIR}/${safe_name}.xml"
  log_path="${RESULT_DIR}/${safe_name}.log"

  echo "EditMode fixture START ${label}"
  printf 'EditMode fixture FILTER'
  printf ' %s' "${batch_fixtures[@]}"
  printf '\n'
  RUN_PLAYMODE=false \
    EDITMODE_RESULTS="${xml_path}" \
    EDITMODE_LOG="${log_path}" \
    EDITMODE_FILTER="${filter}" \
    UNITY_TEST_TIMEOUT_SECONDS="${UNITY_TEST_TIMEOUT_SECONDS}" \
    "${SCRIPT_DIR}/verify-unity-tests.sh"
  exit_code=$?

  if [[ "${exit_code}" -eq 0 ]]; then
    echo "EditMode fixture PASS ${label} XML=${xml_path} LOG=${log_path}"
    batch_fixtures=()
    batch_index=$((batch_index + 1))
    continue
  fi

  if [[ "${exit_code}" -eq 124 ]]; then
    echo "EditMode fixture TIMEOUT ${label} XML=${xml_path} LOG=${log_path}" >&2
    printf 'EditMode timeout fixtures:' >&2
    printf ' %s' "${batch_fixtures[@]}" >&2
    printf '\n' >&2
    exit 124
  fi

  echo "EditMode fixture FAIL ${label} exit=${exit_code} XML=${xml_path} LOG=${log_path}" >&2
  printf 'EditMode failing fixtures:' >&2
  printf ' %s' "${batch_fixtures[@]}" >&2
  printf '\n' >&2
  overall="${exit_code}"
  if [[ "${STOP_ON_FAIL}" == "true" ]]; then
    exit "${exit_code}"
  fi

  batch_fixtures=()
  batch_index=$((batch_index + 1))
done

exit "${overall}"

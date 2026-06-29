#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
MANIFEST_PATH="${UNITY_PROJECT_PATH}/Packages/manifest.json"
PACKAGE_NAME="com.unity.nuget.newtonsoft-json"

if [[ ! -f "${MANIFEST_PATH}" ]]; then
  echo "WARN [NewtonsoftPackageCache][MANIFEST_MISSING] path=${MANIFEST_PATH}"
  exit 0
fi

manifest_count="$(grep -c "\"${PACKAGE_NAME}\"" "${MANIFEST_PATH}" || true)"
if [[ "${manifest_count}" != "1" ]]; then
  echo "WARN [NewtonsoftPackageCache][MANIFEST_ENTRY_COUNT] package=${PACKAGE_NAME} count=${manifest_count}"
  exit 0
fi

package_version="$(
  sed -nE "s/.*\"${PACKAGE_NAME}\"[[:space:]]*:[[:space:]]*\"([^\"]+)\".*/\\1/p" "${MANIFEST_PATH}" \
    | head -1
)"

if [[ -z "${package_version}" ]]; then
  echo "WARN [NewtonsoftPackageCache][VERSION_UNRESOLVED] package=${PACKAGE_NAME}"
  exit 0
fi

package_dir="${PACKAGE_NAME}@${package_version}"
project_package_root="${UNITY_PROJECT_PATH}/Library/PackageCache/${package_dir}"
project_dll="${project_package_root}/Runtime/Newtonsoft.Json.dll"
global_package_root="${UNITY_PACKAGE_CACHE_ROOT:-${HOME}/Library/Unity/cache/packages/packages.unity.com}/${package_dir}"
global_dll="${global_package_root}/Runtime/Newtonsoft.Json.dll"

if [[ -f "${project_dll}" ]]; then
  echo "INFO [NewtonsoftPackageCache][PRESENT] dll=${project_dll}"
  exit 0
fi

if [[ ! -f "${global_dll}" ]]; then
  echo "WARN [NewtonsoftPackageCache][GLOBAL_CACHE_MISSING] dll=${global_dll}"
  exit 0
fi

mkdir -p "${UNITY_PROJECT_PATH}/Library/PackageCache"
tmp_package_root="${project_package_root}.restore.$$"
rm -rf "${tmp_package_root}"
cp -R "${global_package_root}" "${tmp_package_root}"

if [[ ! -f "${tmp_package_root}/Runtime/Newtonsoft.Json.dll" ]]; then
  echo "ERROR [NewtonsoftPackageCache][RESTORE_INCOMPLETE] dll=${tmp_package_root}/Runtime/Newtonsoft.Json.dll" >&2
  rm -rf "${tmp_package_root}"
  exit 1
fi

rm -rf "${project_package_root}"
mv "${tmp_package_root}" "${project_package_root}"
echo "INFO [NewtonsoftPackageCache][RESTORED] source=${global_package_root} target=${project_package_root}"

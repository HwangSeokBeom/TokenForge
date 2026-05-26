#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-${REPO_ROOT}/UnityClient}"
PLUGIN_DYLIB="${PLUGIN_DYLIB:-${UNITY_PROJECT_PATH}/Assets/Plugins/macOS/libDesktopCompanionOverlay.dylib}"
CS_ROOT="${CS_ROOT:-${UNITY_PROJECT_PATH}/Assets/_Project/Scripts}"

if [[ ! -f "${PLUGIN_DYLIB}" ]]; then
  echo "Native plugin dylib not found: ${PLUGIN_DYLIB}" >&2
  exit 1
fi

/usr/bin/python3 - "${CS_ROOT}" "${PLUGIN_DYLIB}" <<'PY'
import pathlib
import re
import subprocess
import sys

cs_root = pathlib.Path(sys.argv[1])
dylib = pathlib.Path(sys.argv[2])

entrypoints = set()
pattern = re.compile(r'DllImport\([^)]*EntryPoint\s*=\s*"([^"]+)"', re.MULTILINE)
for path in cs_root.rglob("*.cs"):
    text = path.read_text(encoding="utf-8", errors="replace")
    entrypoints.update(pattern.findall(text))

nm = subprocess.run(["/usr/bin/nm", "-gU", str(dylib)], text=True, capture_output=True, check=True)
symbols = set()
for line in nm.stdout.splitlines():
    parts = line.split()
    if parts:
        symbols.add(parts[-1].lstrip("_"))

missing = sorted(symbol for symbol in entrypoints if symbol not in symbols)
print(f"TokenForge native symbol check")
print(f"DllImport entrypoints: {len(entrypoints)}")
print(f"Exported symbols: {len(symbols)}")
if missing:
    print("Missing native exports:")
    for symbol in missing:
        print(f"  {symbol}")
    raise SystemExit(2)

print("Result: success")
PY

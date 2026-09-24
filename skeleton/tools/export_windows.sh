#!/usr/bin/env bash
# export_windows.sh — M13 installer-package gate (contract C16) for Last Animal
# (MC 1344, gunilla, 2026-09-24).
#
# Proves, in order:
#   (1) the FULL-GAME Windows release export produces a PE .exe carrying the C#
#       assembly (delegated to ci/export_check.sh, which owns those checks);
#   (2) the artifact is packaged into a distributable .zip — the .exe AND the
#       .NET assemblies data dir beside it (the Windows mono template loads
#       hostfxr + the assemblies from disk, so an exe-only zip cannot launch);
#   (3) a headless smoke of the packaged binary — via wine when available.
#       When wine is NOT available this is stated explicitly on stdout and the
#       script still exits 0: the PE + embedded-assembly checks in
#       export_check.sh are then the executed evidence. It NEVER fakes a launch.
#       The smoke asserts only exit 0 — the C16 ">0 non-blank frames" leg needs
#       a wine-capable host and is not asserted here.
#
# Usage:  bash tools/export_windows.sh
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="$(cd "$HERE/.." && pwd)"
OUT_EXE="build/LastAnimal.exe"
OUT_ZIP="build/LastAnimal-windows-x86_64.zip"

# shellcheck source=../ci/toolchain.sh
source "$PROJ/ci/toolchain.sh"
: "${GODOT:?set GODOT to the engine binary (see engine/PIN.txt or source ci/toolchain.sh)}"

echo "EXPORT_WINDOWS: step 1/3 — full-game export gate (ci/export_check.sh)"
bash "$PROJ/ci/export_check.sh" Windows "$PROJ" "$OUT_EXE"

cd "$PROJ"
echo "EXPORT_WINDOWS: step 2/3 — packaging $OUT_EXE + .NET data dir -> $OUT_ZIP"
rm -f "$OUT_ZIP"
# The mono export writes the .NET assemblies to data_<Assembly>_<arch>/ beside
# the exe, and the Windows template loader loads hostfxr + the assemblies from
# there (the exe's own error string: "Unable to find the .NET assemblies
# directory.") — an exe-only zip cannot launch, so the data dir is mandatory
# payload and its absence must fail the gate here.
DATA_DIR="$(cd build && ls -d data_*_windows_x86_64 2>/dev/null | head -1 || true)"
[ -n "$DATA_DIR" ] || { echo "EXPORT_WINDOWS: FAIL: no .NET assemblies data dir (build/data_*_windows_x86_64) beside $OUT_EXE" >&2; exit 1; }
# `zip` is not installed on this host; python3's zipfile is the stdlib fallback.
python3 - "$OUT_ZIP" "$OUT_EXE" "build/$DATA_DIR" <<'PY'
import os, sys, zipfile
out_zip, out_exe, data_dir = sys.argv[1], sys.argv[2], sys.argv[3]
with zipfile.ZipFile(out_zip, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(out_exe, arcname=os.path.basename(out_exe))
    for root, _dirs, files in os.walk(data_dir):
        for f in sorted(files):
            p = os.path.join(root, f)
            z.write(p, arcname=os.path.relpath(p, os.path.dirname(data_dir)))
# The gate must go red if either half of the payload is missing from the zip.
names = zipfile.ZipFile(out_zip).namelist()
if os.path.basename(out_exe) not in names:
    sys.exit(f"EXPORT_WINDOWS: FAIL: {os.path.basename(out_exe)} missing from {out_zip}")
if not any(n.startswith("data_") and n.endswith("/LastAnimalPreflight.dll") for n in names):
    sys.exit(f"EXPORT_WINDOWS: FAIL: data_*/LastAnimalPreflight.dll missing from {out_zip}")
print(f"EXPORT_WINDOWS: zip payload OK — {len(names)} entries (exe + data dir)")
PY
[ -s "$OUT_ZIP" ] || { echo "EXPORT_WINDOWS: FAIL: $OUT_ZIP is empty" >&2; exit 1; }

echo "EXPORT_WINDOWS: step 3/3 — packaged-binary smoke"
if command -v wine >/dev/null 2>&1; then
  echo "EXPORT_WINDOWS: wine found ($(wine --version 2>/dev/null | head -1)); smoke-launching headless"
  # Headless smoke: the game must start and render; we give it a fixed tick
  # budget via --quit-after and require exit 0.
  timeout 120 wine "$OUT_EXE" --headless --quit-after 120 >/dev/null 2>&1
  echo "EXPORT_WINDOWS: wine smoke PASS (exit 0)"
else
  echo "EXPORT_WINDOWS: wine UNAVAILABLE on this host — packaged-binary launch smoke NOT run (not faked)."
  echo "EXPORT_WINDOWS: executed evidence for the binary is export_check.sh's PE-magic + embedded-C#-assembly checks."
fi

echo "EXPORT_WINDOWS: PASS — $OUT_ZIP ($(stat -c%s "$OUT_ZIP") bytes)"

#!/usr/bin/env bash
# export_check.sh — M00 export-pipeline gate for Last Animal (MC 839.1, gunilla, 2026-08-30).
#
# Proves: (1) export templates for 4.7.2.stable are installed for the calling user
#         (2) headless Windows release export of the given preset PRODUCES a .exe
#             whose first 2 bytes are 'MZ' (valid PE header).
# CI contract for M13 (installer): same two checks at full-game scale.
#
# Usage:
#   GODOT=/path/to/godot ./ci/export_check.sh [preset_name] [project_dir] [out_exe]
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PRESET="${1:-Windows}"
PROJ="${2:-$(dirname "$HERE")}"
OUT_EXE="${3:-build/preflight.exe}"
: "${GODOT:?set GODOT to the engine binary (see engine/PIN.txt)}"

fail() { echo "EXPORT_CHECK: FAIL: $*" >&2; exit 1; }

VER="$($GODOT --version 2>/dev/null | tail -1)"
TDIR="$HOME/.local/share/godot/export_templates"
echo "EXPORT_CHECK: godot=$VER  templates=$TDIR"
[ -d "$TDIR" ] || fail "no export templates dir at $TDIR (install .tpz -> $TDIR/<version>)"

cd "$PROJ" || fail "cannot cd $PROJ"
rm -rf .godot build && mkdir -p build

# Import + build the C# assembly THROUGH THE ENGINE. `--quit` does NOT build the
# solution; without --build-solutions a mono project exports a .exe that is missing
# its assembly (or carries a stale one from an earlier build) and still reports PASS.
timeout 300 "$GODOT" --headless --path . --build-solutions --quit-after 2 >/dev/null 2>&1 \
  || fail "engine --build-solutions failed (see ci/smoke.sh for the readable error)"
timeout 300 "$GODOT" --headless --path . --export-release "$PRESET" "$OUT_EXE" 2>&1 | grep -E "ERROR" && fail "export reported ERROR"
[ -f "$OUT_EXE" ] || fail "export produced no $OUT_EXE"
MAGIC="$(head -c 2 "$OUT_EXE")"
[ "$MAGIC" = "MZ" ] || fail "$OUT_EXE is not a PE (magic='$MAGIC')"

# A PE alone is NOT proof of a C# export: also require the assembly + the
# godot-mono runtime config inside the shipped .exe (embed_pck=true).
ASSEMBLY_NAME="$(sed -n 's|.*<AssemblyName>\(.*\)</AssemblyName>.*|\1|p' *.csproj | head -1)"
[ -n "$ASSEMBLY_NAME" ] || fail "no <AssemblyName> in *.csproj - cannot prove a C# export"
python3 - "$OUT_EXE" "$ASSEMBLY_NAME" <<'PY' || fail "exported .exe does not carry the C# build (see message above)"
import sys
data = open(sys.argv[1], 'rb').read()
name = sys.argv[2].encode()
for tok, why in ((name, 'project assembly'),
                 (b'GodotSharp', 'GodotSharp runtime'),
                 (b'.godot/mono', 'godot-mono runtimeconfig')):
    if tok not in data:
        sys.exit(f"missing {why} marker {tok!r} in {sys.argv[1]} - GDScript-only export?")
    print(f"EXPORT_CHECK: found {why} marker {tok.decode()} x{data.count(tok)}")
PY
echo "EXPORT_CHECK: PASS — $OUT_EXE ($(stat -c%s "$OUT_EXE") bytes, PE magic OK)"

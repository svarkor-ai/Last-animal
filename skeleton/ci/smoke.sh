#!/usr/bin/env bash
# smoke.sh — M00 headless smoke gate for Last Animal (MC 839.1, gunilla, 2026-08-30).
#
# CI contract for every later module (M01..M13): a build is a SMOKE PASS iff
#   (a) this script exits 0, AND
#   (b) graphical-test-helper.sh reports RESULT=PASS (non-blank framebuffer).
# Both halves are required. `godot --headless --quit` exiting 0 alone does NOT
# prove rendering (see PREFLIGHT.md lesson 3).
#
# Usage:
#   GODOT=/path/to/godot ./ci/smoke.sh [project_dir]     (project_dir defaults to repo root = dir above ci/)
#   graphical-test-helper.sh is expected on PATH (vm105: /usr/local/bin).
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
: "${GODOT:?set GODOT to the engine binary (see engine/PIN.txt)}"

fail() { echo "SMOKE: FAIL: $*" >&2; exit 1; }
[ -x "$GODOT" ] || fail "GODOT not executable: $GODOT"
[ -f "$PROJ/project.godot" ] || fail "not a godot project: $PROJ"
command -v graphical-test-helper.sh >/dev/null || fail "graphical-test-helper.sh not on PATH"

# (a-1) clean-clone import: a fresh clone has an empty/missing .godot/imported/,
# so the first gate run spams import errors and the navmesh bakes 0 polygons.
# Import once when needed (idempotent; cheap no-op when already imported).
if [ ! -d "$PROJ/.godot/imported" ] || [ -z "$(ls -A "$PROJ/.godot/imported" 2>/dev/null)" ]; then
  echo "SMOKE: .godot/imported empty — running one-time --import"
  timeout 600 "$GODOT" --headless --path "$PROJ" --import \
    || fail "engine --import exited nonzero"
fi

echo "SMOKE: godot=$($GODOT --version 2>/dev/null | tail -1)  project=$PROJ"

# (a0) build the C# assembly through the ENGINE, not a bare `dotnet build`.
# A `--headless --quit` run does NOT build the solution on its own: it loads the
# last-built assembly, so a missing/stale assembly prints
#   "Cannot instantiate C# script ... class could not be found"
# and STILL renders the scene (Cube + light are pure scene nodes), so the
# framebuffer gate below would PASS with ZERO scripts executing. --build-solutions
# forces the engine's own build step, which is the only thing that proves the
# pinned mono toolchain (not our shell) can compile the project.
timeout 300 "$GODOT" --headless --path "$PROJ" --build-solutions --quit-after 2 \
  || fail "engine --build-solutions exited nonzero"

# (a) headless load+quit — the process must exit 0, and NO C# load error is tolerated
LOAD_LOG="$(timeout 120 "$GODOT" --headless --path "$PROJ" --quit-after 3 2>&1)" \
  || fail "headless quit exited nonzero"
# bash-native case-insensitive substring check (no pipe => no SIGPIPE race under
# pipefail: grep -q exits on first match and SIGPIPEs the writer, which pipefail
# turns into a spurious pipeline failure on large logs).
LOAD_LOG_LC="${LOAD_LOG,,}"
if [[ "$LOAD_LOG_LC" == *'could not be found'* || "$LOAD_LOG_LC" == *'cannot instantiate c#'* ]]; then
  fail "C# script did NOT load (assembly missing or class name mismatch):
$LOAD_LOG"
fi
# Since w3 (MC 1123.9) run/main_scene points at res://main.tscn, the preflight
# scene never loads and its 'C# scene alive' marker can never print; the boot
# marker is now the composition root's GameLoop line.
[[ "$LOAD_LOG" == *'GameLoop: ready'* ]] \
  || fail "composition root did not boot (expected 'GameLoop: ready' in output)"

# (b) render-and-verify under throwaway Xvfb — non-blank framebuffer required
OUT="$(mktemp -u /tmp/smoke_XXXXXX.png)"
graphical-test-helper.sh --cmd "$GODOT --path $PROJ" --wait 6 --out "$OUT" || { rm -f "$OUT"; fail "framebuffer gate failed (see RESULT line above)"; }
mv -f "$OUT" "${TMPDIR:-/tmp}/last-animal-smoke.png" 2>/dev/null || true
rm -f "$OUT"

echo "SMOKE: PASS (headless quit + framebuffer) — capture at /tmp/last-animal-smoke.png"

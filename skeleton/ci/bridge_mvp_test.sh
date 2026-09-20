#!/usr/bin/env bash
# bridge_mvp_test.sh — W7 playable-MVP acceptance gate (MC 1123.14; redo of MC 1123.8).
#
# Proves the FULL playable MVP loop (BRIDGE-MVP.md §3) with all six markers:
#   PLAYER_MOVED, DNA_EXTRACTED, HUD_BOUND, BOOK_OPENED, COMPANION_FOLLOWS,
#   SAVE_ROUNDTRIP — plus the non-blank framebuffer render bar.
#
# Mirrors ci/main_composition_test.sh's FIXED pattern (MC 1123.10 follow-up):
#   - one-time --import when .godot/imported is empty (clean-clone first run),
#   - build through the pinned engine (--build-solutions),
#   - headless proof run -> markers asserted via bash substring checks
#     (no `printf | grep -q`: grep -q exits on first match and SIGPIPEs the
#     writer, which pipefail turns into a spurious failure on large logs),
#   - graphical-test-helper render bar with --wait 15 (8-9s catches only the
#     engine splash; 15s reaches the live scene — MC 1123.12 lesson),
#   - GATE PASS + exit 0.
#
# Usage:
#   GODOT=/path/to/godot ./ci/bridge_mvp_test.sh [project_dir]  (defaults to dir above ci/)
#   GUI_HELPER=... overrides the graphical-test-helper path (default /usr/local/bin).
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
PROOF="res://ci_proofs/BridgeMvpProof.cs"
GUI_HELPER="${GUI_HELPER:-/usr/local/bin/graphical-test-helper.sh}"
PASS_MARKER="BRIDGE_MVP_PROOF: PASS"

fail() { echo "BRIDGE_MVP_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -f "$PROJ/ci_proofs/BridgeMvpProof.cs" ] || fail "BridgeMvpProof.cs not found"
[ -f "$PROJ/main.tscn" ] || fail "main.tscn not found (composition root missing)"
: "${GODOT:?set GODOT to the engine binary (see ../engine/PIN.txt)}"
[ -x "$GODOT" ] || fail "GODOT not executable: $GODOT"
[ -f "$GUI_HELPER" ] || fail "graphical-test-helper not found: $GUI_HELPER"

# (A-1) clean-clone import: a fresh clone has an empty/missing .godot/imported/,
# so the first gate run spams import errors and the navmesh bakes 0 polygons.
# Import once when needed (idempotent; cheap no-op when already imported).
if [ ! -d "$PROJ/.godot/imported" ] || [ -z "$(ls -A "$PROJ/.godot/imported" 2>/dev/null)" ]; then
  echo "BRIDGE_MVP_TEST: .godot/imported empty — running one-time --import"
  timeout 600 "$GODOT" --headless --path "$PROJ" --import \
    || fail "engine --import exited nonzero"
fi

echo "BRIDGE_MVP_TEST: project=$PROJ  godot=$("$GODOT" --version 2>/dev/null | tail -1)"

# build through the pinned engine so the C# assembly (incl. the proof) is current
timeout 300 "$GODOT" --headless --path "$PROJ" --build-solutions --quit-after 1 \
  || fail "engine --build-solutions exited nonzero (C# build failed)"

# (A) headless proof: all six markers asserted in-code -> exit 0 + PASS marker
LOGA="$(timeout 180 "$GODOT" --headless --path "$PROJ" --script "$PROOF" 2>&1)"
CODEA=$?
printf '%s\n' "$LOGA"
[ "$CODEA" -eq 0 ] || fail "headless proof exited $CODEA (non-zero)"
# bash-native substring checks (no pipe => no SIGPIPE race under pipefail)
[[ "$LOGA" == *"$PASS_MARKER"* ]] \
  || fail "expected '$PASS_MARKER' marker (a marker assert went red)"
[[ "$LOGA" == *'PLAYER_MOVED'* ]] \
  || fail "expected 'PLAYER_MOVED' (simulated WASD -> PlayerController -> body)"
[[ "$LOGA" == *'DNA_EXTRACTED'* ]] \
  || fail "expected 'DNA_EXTRACTED' (kill -> OnKill -> EventBus.DnaExtracted)"
[[ "$LOGA" == *'HUD_BOUND'* ]] \
  || fail "expected 'HUD_BOUND' (Hud bound to the bus, gauges move on events)"
[[ "$LOGA" == *'BOOK_OPENED'* ]] \
  || fail "expected 'BOOK_OPENED' (EmpathyPanel.Open surfaced a real M04 BookEntry)"
[[ "$LOGA" == *'COMPANION_FOLLOWS'* ]] \
  || fail "expected 'COMPANION_FOLLOWS' (companion closed on the player over frames)"
[[ "$LOGA" == *'SAVE_ROUNDTRIP'* ]] \
  || fail "expected 'SAVE_ROUNDTRIP' (SaveSystem.Save -> Load via GodotSaveStore)"

# (B) framebuffer render bar: the playable scene actually paints a non-blank frame.
# --wait 15: 8-9s catches only the engine splash (MC 1123.12 capture lesson); the
# proof holds the live scene after PASS so 15s lands on real scene content.
LOGC="$("$GUI_HELPER" --cmd "$GODOT --path $PROJ --script $PROOF" --wait 15 2>&1)"
CODEc=$?
printf '%s\n' "$LOGC"
[ "$CODEc" -eq 0 ] || fail "graphical-test-helper exited $CODEc (render bar not met)"
[[ "$LOGC" == *'RESULT=PASS'* ]] \
  || fail "expected 'RESULT=PASS' from graphical-test-helper (framebuffer blank/uniform)"

echo "BRIDGE_MVP_TEST: GATE PASS — six-marker playable MVP verified (PLAYER_MOVED, DNA_EXTRACTED, HUD_BOUND, BOOK_OPENED, COMPANION_FOLLOWS, SAVE_ROUNDTRIP; non-blank render)"
exit 0

#!/usr/bin/env bash
# main_composition_test.sh — M01 bridge CARD 3 composition-root DoD gate (MC 1123.9).
#
# Proves res://main.tscn is a PLAYABLE composition root (BRIDGE-MVP.md §2 STEP C):
#   RG2 closed  : project.godot run/main_scene points at main.tscn (not preflight).
#   Scene loads : main.tscn instantiates the meadow zone + Player CharacterBody3D +
#                 isometric Camera3D (the whole tree resolves + the pure-logic
#                 PlayerController sidecar boots).
#   Player moves: simulated WASD (the [input] map from 1123.2/RG3) drives
#                 PlayerController.Move -> CharacterBody3D XZ displacement.
#   Cam follows : the CameraRig (isometric Camera3D) tracks the player.
#   Renders     : graphical-test-helper non-blank framebuffer (the playable-bar).
#
# Run: ./ci/main_composition_test.sh [project_dir]   (defaults to dir above ci/)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
PROOF="res://ci_proofs/MainCompositionProof.cs"
GUI_HELPER="${GUI_HELPER:-/usr/local/bin/graphical-test-helper.sh}"
PASS_MARKER="MAIN_COMPOSITION_PROOF: PASS"

fail() { echo "MAIN_COMPOSITION_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -f "$PROJ/ci_proofs/MainCompositionProof.cs" ] || fail "MainCompositionProof.cs not found"
[ -f "$PROJ/main.tscn" ] || fail "main.tscn not found (composition root missing)"
: "${GODOT:?set GODOT to the engine binary (see ../engine/PIN.txt)}"
[ -x "$GODOT" ] || fail "GODOT not executable: $GODOT"
[ -f "$GUI_HELPER" ] || fail "graphical-test-helper not found: $GUI_HELPER"

# (A-1) clean-clone import: a fresh clone has an empty/missing .godot/imported/,
# so the first gate run spams import errors and the navmesh bakes 0 polygons.
# Import once when needed (idempotent; cheap no-op when already imported).
if [ ! -d "$PROJ/.godot/imported" ] || [ -z "$(ls -A "$PROJ/.godot/imported" 2>/dev/null)" ]; then
  echo "MAIN_COMPOSITION_TEST: .godot/imported empty — running one-time --import"
  timeout 600 "$GODOT" --headless --path "$PROJ" --import \
    || fail "engine --import exited nonzero"
fi

echo "MAIN_COMPOSITION_TEST: project=$PROJ  godot=$("$GODOT" --version 2>/dev/null | tail -1)"

# build through the pinned engine so the C# assembly (Player.cs + proof) is current
timeout 300 "$GODOT" --headless --path "$PROJ" --build-solutions --quit-after 1 \
  || fail "engine --build-solutions exited nonzero (C# build failed)"

# (A) headless proof: scene loads + player moves + camera follows -> exit 0 + PASS marker
LOGA="$(timeout 120 "$GODOT" --headless --path "$PROJ" --script "$PROOF" 2>&1)"
CODEA=$?
printf '%s\n' "$LOGA"
[ "$CODEA" -eq 0 ] || fail "headless proof exited $CODEA (non-zero)"
# bash-native substring checks (no pipe => no SIGPIPE race under pipefail:
# grep -q exits on first match and SIGPIPEs the writer, which pipefail turns
# into a spurious pipeline failure on large logs).
[[ "$LOGA" == *"$PASS_MARKER"* ]] \
  || fail "expected '$PASS_MARKER' marker (a composition-root check went red)"
[[ "$LOGA" == *'RG2 OK'* ]] \
  || fail "expected 'RG2 OK' (run/main_scene must point at main.tscn)"
[[ "$LOGA" == *'PLAYER_MOVED'* ]] \
  || fail "expected 'PLAYER_MOVED' (input -> PlayerController -> body)"
[[ "$LOGA" == *'CAMERA_FOLLOWS'* ]] \
  || fail "expected 'CAMERA_FOLLOWS' (isometric Camera3D tracks player)"

# (B) framebuffer render bar: the composition root actually paints a non-blank frame
LOGC="$("$GUI_HELPER" --cmd "$GODOT --path $PROJ --script $PROOF" --wait 6 2>&1)"
CODEc=$?
printf '%s\n' "$LOGC"
[ "$CODEc" -eq 0 ] || fail "graphical-test-helper exited $CODEc (render bar not met)"
[[ "$LOGC" == *'RESULT=PASS'* ]] \
  || fail "expected 'RESULT=PASS' from graphical-test-helper (framebuffer blank/uniform)"

echo "MAIN_COMPOSITION_TEST: GATE PASS — composition root playable (RG2+RG3+C10+C17; non-blank render)"
exit 0

#!/usr/bin/env bash
# audio_test.sh — M06 audio headless DoD gate (MC 890.7, artemis, 2026-09-03).
#
# The Phase-7 DoD (PHASE0.md Phase 7): a headless test fires an EventBus signal that
# triggers a non-silent SFX on the correct bus; CC0 SFX/music files present with
# licence/attribution noted.
#
# This script mirrors the M01 boot_test.sh + M02 dna_npc_test.sh contract (same
# invocation shape). It:
#   (1) builds the C# assembly THROUGH the pinned engine (like boot_test.sh) so a
#       missing/wrong asset or a compile error fails the gate, not passes it;
#   (2) runs the headless tests/AudioTest.cs through the engine and requires
#       VERIFY_EXIT=0 AND the 'M06_AUDIO_TEST: PASS' marker present.
#
# Usage:
#   GODOT=/path/to/engine ./ci/audio_test.sh [project_dir]   (defaults to dir above ci/)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
: "${GODOT:?set GODOT to the engine binary (see ../engine/PIN.txt)}"

fail() { echo "M06_AUDIO_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -x "$GODOT" ] || fail "GODOT not executable: $GODOT"
[ -f "$PROJ/project.godot" ] || fail "not a godot project: $PROJ"

echo "M06_AUDIO_TEST: godot=$("$GODOT" --version 2>/dev/null | tail -1)  project=$PROJ"

# (1) Build through the engine (proves the pinned mono toolchain compiles our M06 code;
#     a bare --script run would silently use a stale assembly).
timeout 300 "$GODOT" --headless --path "$PROJ" --build-solutions --quit-after 1 \
  || fail "engine --build-solutions exited nonzero (C# build failed)"

# (2) Run the headless audio DoD test. The test self-reports each check + a final
#     PASS/FAIL line, then Quits with that code. (Assets must already be imported
#     once via `--import`; if a Load fails the test's checks catch it and go red.)
LOG="$(timeout 180 "$GODOT" --headless --path "$PROJ" --script res://tests/AudioTest.cs 2>&1)"
CODE=$?
printf '%s\n' "$LOG"

# (3) The gate: exit code 0 AND the PASS marker present. Either missing => fail.
[ "$CODE" -eq 0 ] || fail "AudioTest exited $CODE (non-zero)"
printf '%s\n' "$LOG" | grep -q 'M06_AUDIO_TEST: PASS' \
  || fail "expected 'M06_AUDIO_TEST: PASS' marker in output (a check went red)"

echo "M06_AUDIO_TEST: GATE PASS — EventBus-triggered non-silent SFX on the Sfx bus, music on Music bus"
exit 0

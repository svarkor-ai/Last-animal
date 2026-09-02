#!/usr/bin/env bash
# boot_test.sh — M01 core-framework headless boot gate (MC 839.3, teddy, 2026-09-01).
#
# The Phase-3 DoD (PHASE0.md Phase 3): instantiate GameBootstrap + EventBus, fire
# ONE global signal with a subscriber receiving it, exit 0; AND prove C3 (DI
# Bind/Resolve round-trip + boot order = registration order). This runs the
# real headless BootTest.cs through the pinned engine — no scene, no render, no X.
#
# The gate is meaningful ONLY if it can fail: the script asserts the engine's
# exit code AND greps the output for the PASS marker, so a missing assembly, a
# crashed _Ready, or a failed check all exit non-zero.
#
# Usage:
#   GODOT=/path/to/godot ./ci/boot_test.sh [project_dir]   (defaults to dir above ci/)
#   (or: bash ci/boot_test.sh from the repo root, matching smoke.sh's contract)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
: "${GODOT:?set GODOT to the engine binary (see ../engine/PIN.txt)}"

fail() { echo "BOOT_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -x "$GODOT" ] || fail "GODOT not executable: $GODOT"
[ -f "$PROJ/project.godot" ] || fail "not a godot project: $PROJ"

echo "BOOT_TEST: godot=$($GODOT --version 2>/dev/null | tail -1)  project=$PROJ"

# (1) Build the C# assembly THROUGH THE ENGINE (the only thing that proves the
# pinned mono toolchain compiles the project — a bare --script run loads the
# last-built assembly and would pass with zero of our code, PREFLIGHT.md L2).
timeout 300 "$GODOT" --headless --path "$PROJ" --build-solutions --quit-after 1 \
  || fail "engine --build-solutions exited nonzero (C# build failed)"

# (2) Run the headless boot test as a script: no scene, no render, no X. The
# test self-reports checks + a final PASS/FAIL line, then Quits with that code.
LOG="$(timeout 180 "$GODOT" --headless --path "$PROJ" --script res://tests/BootTest.cs 2>&1)"
CODE=$?
printf '%s\n' "$LOG"

# (3) The gate: exit code 0 AND the PASS marker present. Either missing => fail.
[ "$CODE" -eq 0 ] || fail "BootTest exited $CODE (non-zero)"
printf '%s\n' "$LOG" | grep -q 'BOOT_TEST: PASS signal received' \
  || fail "expected 'BOOT_TEST: PASS signal received' in output (C2 signal not received or boot failed)"

echo "BOOT_TEST: GATE PASS — signal received, DI round-trip, boot order = registration order"
exit 0

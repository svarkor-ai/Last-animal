#!/usr/bin/env bash
# ui_test.sh — M10 ui-hud DoD gate (MC 890.14, dobbie, 2026-09-06).
#
# The Phase-11 DoD (PHASE0.md Phase 11, C13):
#   "headless render of the HUD scene is non-blank with all bound values
#    updated from EventBus (graphical-test-helper exit 0); EmpathyPanel opens
#    a real BookEntry read from M04."
#
# This gate proves the C13 contract on the Godot side (the UI modules live in
# src/ui/ and are compiled by the main engine project). Following the fleet's
# two-sided calibration discipline:
#   (A) run the render test HEADLESS  -> exit 0 AND the 'M10_UI_RENDER_TEST:
#       PASS' marker. Its in-code checks assert the C13 surface: the HUD binds
#       four gauges and moves DnaMeter (DnaExtracted/DnaSpoken) + CompanionHearts
#       (LoyaltyChanged) FROM the EventBus; DialogueSystem.Show paints a node;
#       EmpathyPanel.Open surfaces a REAL M04 BookEntry read from EmpathyBook.Query.
#   (B) run the UiHarnessSelfTest     -> must exit NON-zero (deliberately broken;
#       proves the gate can fail — not a rubber stamp).
#   (C) run the UI under graphical-test-helper (Xvfb) -> RESULT=PASS means the
#       framebuffer is non-blank (the Phase-11 DoD's named render bar).
# The gate passes only if A, B and C all behave as expected.
#
# Usage:
#   ./ci/ui_test.sh [project_dir]   (defaults to dir above ci/)
#   GUI_HELPER=... to override the graphical-test-helper path (default /usr/local/bin).
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
TESTS_UI="$PROJ/tests/ui"
GUI_HELPER="${GUI_HELPER:-/usr/local/bin/graphical-test-helper.sh}"
UI_RENDER="res://tests/ui/UiRenderTest.cs"
UI_HARNESS="res://tests/ui/UiHarnessSelfTest.cs"
PASS_MARKER="M10_UI_RENDER_TEST: PASS"

fail() { echo "UI_HUD_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -d "$TESTS_UI" ] || fail "ui tests dir not found: $TESTS_UI"
[ -f "$TESTS_UI/UiRenderTest.cs" ] || fail "UiRenderTest.cs not found"
[ -f "$TESTS_UI/UiHarnessSelfTest.cs" ] || fail "UiHarnessSelfTest.cs not found"
: "${GODOT:?set GODOT to the engine binary (see ../engine/PIN.txt)}"
[ -x "$GODOT" ] || fail "GODOT not executable: $GODOT"
[ -f "$GUI_HELPER" ] || fail "graphical-test-helper not found: $GUI_HELPER (install per infra/vm105)"

echo "UI_HUD_TEST: project=$PROJ  godot=$("$GODOT" --version 2>/dev/null | tail -1)"

# ---------------------------------------------------------------------------
# build through the pinned engine so the C# assembly (incl. src/ui/) is current
# ---------------------------------------------------------------------------
echo "UI_HUD_TEST: build through engine"
timeout 300 "$GODOT" --headless --path "$PROJ" --build-solutions --quit-after 1 \
  || fail "engine --build-solutions exited nonzero (C# build failed)"

# ---------------------------------------------------------------------------
# (A) headless C13 assertions — render test must exit 0 + PASS marker
# ---------------------------------------------------------------------------
echo "UI_HUD_TEST: run A — headless render test (expect exit 0 + PASS)"
LOGA="$(timeout 180 "$GODOT" --headless --path "$PROJ" --script "$UI_RENDER" 2>&1)"
CODEA=$?
printf '%s\n' "$LOGA"
[ "$CODEA" -eq 0 ] || fail "run A: render test exited $CODEA (non-zero)"
# bash-native substring checks (no pipe => no SIGPIPE race under pipefail)
[[ "$LOGA" == *"$PASS_MARKER"* ]] \
  || fail "run A: expected '$PASS_MARKER' marker (a C13 check went red)"

# ---------------------------------------------------------------------------
# (B) two-sided calibration — harness self-test must go red (non-zero)
# ---------------------------------------------------------------------------
echo "UI_HUD_TEST: run B — harness self-test (expect non-zero exit)"
LOGB="$(timeout 60 "$GODOT" --headless --path "$PROJ" --script "$UI_HARNESS" 2>&1)"
CODEB=$?
printf '%s\n' "$LOGB"
[ "$CODEB" -ne 0 ] || fail "run B: harness self-test exited 0 (gate cannot fail — broken)"

# ---------------------------------------------------------------------------
# (C) framebuffer render proof — graphical-test-helper must report RESULT=PASS
# ---------------------------------------------------------------------------
echo "UI_HUD_TEST: run C — graphical-test-helper non-blank render (expect RESULT=PASS)"
LOGC="$("$GUI_HELPER" --cmd "$GODOT --path $PROJ --script $UI_RENDER" --wait 3 2>&1)"
CODEc=$?
printf '%s\n' "$LOGC"
[ "$CODEc" -eq 0 ] || fail "run C: graphical-test-helper exited $CODEc (non-blank render bar not met)"
[[ "$LOGC" == *'RESULT=PASS'* ]] \
  || fail "run C: expected 'RESULT=PASS' from graphical-test-helper (framebuffer blank)"

echo "UI_HUD_TEST: GATE PASS — C13 Hud/Dialogue/EmpathyPanel checks green (A), harness self-test red (B), non-blank framebuffer render (C)"
exit 0

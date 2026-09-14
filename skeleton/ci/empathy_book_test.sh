#!/usr/bin/env bash
# empathy_book_test.sh — M04 empathy-book DoD gate (MC 890.11, dobbie, 2026-09-06).
#
# The Phase-8 DoD (PHASE0.md Phase 8, C9):
#   "unit tests for Query + RouteResolution PASS (exit 0) incl. the
#    Forgive-vs-PermanentBreak branch and a deliberately-failing case;
#    `EmpathyBookOpened()` signal fires (C2)."
#
# This gate mirrors the dna_npc_test.sh two-sided calibration contract AND the
# audio_test.sh godot-signal contract, applied to the independent M04 suite
# (tests/empathy/LastAnimalEmpathyTests.csproj):
#   (A) xunit: run the M04 suite TWICE —
#       (A1) WITH EmpathyHarnessSelfTest.cs  -> exactly 1 failure (the
#            deliberately-broken case; proves the harness can fail);
#       (A2) WITHOUT it                      -> 0 failures (real tests green).
#   (B) godot: build through the pinned engine, then run tests/EmpathySignalTest.cs
#       headlessly and require exit 0 + the 'M04_EMPATHY_SIGNAL_TEST: PASS' marker
#       (proves `EmpathyBookOpened()` fires to a subscriber on the C2 bus).
# The gate passes only if ALL of A1, A2 and B behave as expected.
#
# Usage:
#   GODOT=/path/to/engine ./ci/empathy_book_test.sh [project_dir]
#        (defaults to dir above ci/)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
TESTS_DIR="$PROJ/tests"
EMPATHY_DIR="$TESTS_DIR/empathy"
PROJCS="LastAnimalEmpathyTests.csproj"
SELFTEST="$EMPATHY_DIR/EmpathyHarnessSelfTest.cs"

fail() { echo "M04_EMPATHY_BOOK: GATE FAIL: $*" >&2; exit 1; }
[ -d "$EMPATHY_DIR" ] || fail "M04 tests dir not found: $EMPATHY_DIR"
[ -f "$SELFTEST" ] || fail "M04 harness self-test not found: $SELFTEST"
[ -f "$EMPATHY_DIR/$PROJCS" ] || fail "M04 test project not found: $EMPATHY_DIR/$PROJCS"
: "${GODOT:?set GODOT to the engine binary (see ../engine/PIN.txt)}"
[ -x "$GODOT" ] || fail "GODOT not executable: $GODOT"

echo "M04_EMPATHY_BOOK: project=$PROJ  godot=$("$GODOT" --version 2>/dev/null | tail -1)"

# ---------------------------------------------------------------------------
# (A) Standalone xunit suite — two-sided calibration
# ---------------------------------------------------------------------------
echo "M04_EMPATHY_BOOK: run A1 — with harness self-test (expect 1 failure)"
LOG1="$(cd "$PROJ" && dotnet test "$EMPATHY_DIR/$PROJCS" --no-restore 2>&1)"
CODE1=$?
printf '%s\n' "$LOG1"
[ "$CODE1" -ne 0 ] || fail "A1: expected non-zero exit (harness self-test should fail), got 0"

# xunit summary lines: "Failed!  - Failed:     1, Passed:    16, Skipped:     0, Total:    17"
FAILED1=$(printf '%s\n' "$LOG1" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED1=$(printf '%s\n' "$LOG1" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL1=$(printf '%s\n' "$LOG1" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "M04_EMPATHY_BOOK: run A1 — Failed=$FAILED1 Passed=$PASSED1 Total=$TOTAL1"
[ "$FAILED1" = "1" ] || fail "A1: expected exactly 1 failure (the deliberately-broken case), got $FAILED1"
[ "$PASSED1" != "" ] || fail "A1: no passed count found in output"

echo "M04_EMPATHY_BOOK: run A2 — without harness self-test (expect 0 failures)"
mv "$SELFTEST" "$SELFTEST.bak"
LOG2="$(cd "$PROJ" && dotnet test "$EMPATHY_DIR/$PROJCS" --no-restore 2>&1)"
CODE2=$?
mv "$SELFTEST.bak" "$SELFTEST"
printf '%s\n' "$LOG2"
[ "$CODE2" -eq 0 ] || fail "A2: expected exit 0 (all tests green), got $CODE2"

FAILED2=$(printf '%s\n' "$LOG2" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED2=$(printf '%s\n' "$LOG2" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL2=$(printf '%s\n' "$LOG2" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "M04_EMPATHY_BOOK: run A2 — Failed=$FAILED2 Passed=$PASSED2 Total=$TOTAL2"
[ "$FAILED2" = "0" ] || fail "A2: expected 0 failures, got $FAILED2"
[ "$TOTAL1" = "$((TOTAL2 + 1))" ] || fail "A1/A2 total mismatch: run1=$TOTAL1 run2=$TOTAL2 (expected run1 = run2 + 1)"

# ---------------------------------------------------------------------------
# (B) Godot headless — EmpathyBookOpened() fired to a subscriber (C2)
# ---------------------------------------------------------------------------
echo "M04_EMPATHY_BOOK: build through engine, then run EmpathySignalTest.cs"
timeout 300 "$GODOT" --headless --path "$PROJ" --build-solutions --quit-after 1 \
  || fail "engine --build-solutions exited nonzero (C# build failed)"

LOG3="$(timeout 180 "$GODOT" --headless --path "$PROJ" --script res://tests/EmpathySignalTest.cs 2>&1)"
CODE3=$?
printf '%s\n' "$LOG3"
[ "$CODE3" -eq 0 ] || fail "EmpathySignalTest exited $CODE3 (non-zero)"
# bash-native substring check (no pipe => no SIGPIPE race under pipefail)
[[ "$LOG3" == *'M04_EMPATHY_SIGNAL_TEST: PASS'* ]] \
  || fail "expected 'M04_EMPATHY_SIGNAL_TEST: PASS' marker in output (a check went red)"

echo "M04_EMPATHY_BOOK: GATE PASS — Query+RouteResolution suite green ($PASSED2 passed, 0 failed), harness self-test red, EmpathyBookOpened() fired (C2)"
exit 0

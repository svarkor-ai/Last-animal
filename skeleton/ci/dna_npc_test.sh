#!/usr/bin/env bash
# dna_npc_test.sh — M02+M03 pure-logic test gate (MC 890.2, dobbie, 2026-09-03).
#
# The Phase-5 DoD (PHASE0.md Phase 5): `dotnet test` over the dna_language +
# npc_emotion module test suites PASSES (exit 0), including ONE
# deliberately-broken case that goes red (harness self-test; two-sided
# calibration).
#
# This script runs the test suite TWICE:
#   (1) WITH the harness self-test file present -> the suite must report
#       exactly 1 failure (the deliberately-broken test) and all other tests
#       green. This proves the harness CAN fail.
#   (2) WITHOUT the harness self-test file -> the suite must report 0
#       failures. This proves the real tests are all green.
#
# The gate passes only if BOTH runs behave as expected.
#
# Usage:
#   ./ci/dna_npc_test.sh [project_dir]   (defaults to dir above ci/)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
TESTS_DIR="$PROJ/tests"
SELFTEST="$TESTS_DIR/HarnessSelfTest.cs"

fail() { echo "DNA_NPC_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -d "$TESTS_DIR" ] || fail "tests dir not found: $TESTS_DIR"
[ -f "$SELFTEST" ] || fail "harness self-test not found: $SELFTEST"

echo "DNA_NPC_TEST: project=$PROJ"

# (1) Run WITH the harness self-test present.
#     Expected: exactly 1 failure (the deliberately-broken test), all others green.
echo "DNA_NPC_TEST: run 1 — with harness self-test (expect 1 failure)"
LOG1="$(cd "$PROJ" && dotnet test "$TESTS_DIR/LastAnimalDnaNpcTests.csproj" --no-restore 2>&1)"
CODE1=$?
printf '%s\n' "$LOG1"

# The deliberately-broken test must have FAILED (non-zero exit).
[ "$CODE1" -ne 0 ] || fail "run 1: expected non-zero exit (harness self-test should fail), got 0"

# The output must show exactly 1 failed test.
# xunit summary line format: "Failed!  - Failed:     1, Passed:    52, Skipped:     0, Total:    53"
FAILED1=$(printf '%s\n' "$LOG1" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED1=$(printf '%s\n' "$LOG1" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL1=$(printf '%s\n' "$LOG1" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "DNA_NPC_TEST: run 1 — Failed=$FAILED1 Passed=$PASSED1 Total=$TOTAL1"

[ "$FAILED1" = "1" ] || fail "run 1: expected exactly 1 failure, got $FAILED1"
[ "$PASSED1" != "" ] || fail "run 1: no passed count found in output"

# (2) Run WITHOUT the harness self-test.
#     Expected: 0 failures, all tests green.
echo "DNA_NPC_TEST: run 2 — without harness self-test (expect 0 failures)"
mv "$SELFTEST" "$SELFTEST.bak"
LOG2="$(cd "$PROJ" && dotnet test "$TESTS_DIR/LastAnimalDnaNpcTests.csproj" --no-restore 2>&1)"
CODE2=$?
mv "$SELFTEST.bak" "$SELFTEST"
printf '%s\n' "$LOG2"

# The suite must pass (exit 0) with 0 failures.
[ "$CODE2" -eq 0 ] || fail "run 2: expected exit 0 (all tests green), got $CODE2"

FAILED2=$(printf '%s\n' "$LOG2" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED2=$(printf '%s\n' "$LOG2" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL2=$(printf '%s\n' "$LOG2" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "DNA_NPC_TEST: run 2 — Failed=$FAILED2 Passed=$PASSED2 Total=$TOTAL2"

[ "$FAILED2" = "0" ] || fail "run 2: expected 0 failures, got $FAILED2"
[ "$PASSED2" != "" ] || fail "run 2: no passed count found in output"

# The total test count must be the same in both runs (the self-test is the
# only difference; it adds 1 test).
[ "$TOTAL1" = "$((TOTAL2 + 1))" ] || fail "total mismatch: run1=$TOTAL1 run2=$TOTAL2 (expected run1 = run2 + 1)"

echo "DNA_NPC_TEST: GATE PASS — harness self-test went red (1 failure), real suite green ($PASSED2 passed, 0 failed)"
exit 0

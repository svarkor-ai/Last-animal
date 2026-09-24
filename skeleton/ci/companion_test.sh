#!/usr/bin/env bash
# companion_test.sh — M05 companion-system pure-logic test gate (MC 890.12, bernie, 2026-09-06).
#
# The Phase-9 DoD (PHASE0.md Phase 9): "A headless test drives a companion
# through follow -> need -> (unpaid) -> loyalty-drop transitions using M03's
# SalarySystem (exit 0)". Like the Phase-5/Phase-8 gates, this also includes
# ONE deliberately-broken case that goes red (harness self-test; two-sided
# calibration).
#
# This script runs the M05 suite TWICE, mirroring ci/dna_npc_test.sh:
#   (1) WITH the harness self-test -> exactly 1 failure (the broken one).
#   (2) WITHOUT it -> 0 failures (the real suite is green).
# The gate passes only if BOTH runs behave as expected.
#
# Usage:
#   ./ci/companion_test.sh [project_dir]   (defaults to dir above ci/)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
TESTS_DIR="$PROJ/tests/companion"
SELFTEST="$TESTS_DIR/CompanionHarnessSelfTest.cs"
CSPROJ="$TESTS_DIR/LastAnimalCompanionTests.csproj"

fail() { echo "COMPANION_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -d "$TESTS_DIR" ] || fail "companion tests dir not found: $TESTS_DIR"
[ -f "$SELFTEST" ] || fail "harness self-test not found: $SELFTEST"
[ -f "$CSPROJ" ] || fail "test project not found: $CSPROJ"

echo "COMPANION_TEST: project=$PROJ"

# (1) Run WITH the harness self-test present. Expected: 1 failure.
# No --no-restore: it silently no-ops (exit 0, no output) on a never-restored
# clean clone, so the self-test check would read nothing (MC 1344.2).
echo "COMPANION_TEST: run 1 — with harness self-test (expect 1 failure)"
LOG1="$(cd "$PROJ" && dotnet test "$CSPROJ" 2>&1)"
CODE1=$?
printf '%s\n' "$LOG1"

[ "$CODE1" -ne 0 ] || fail "run 1: expected non-zero exit (harness self-test should fail), got 0"

FAILED1=$(printf '%s\n' "$LOG1" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED1=$(printf '%s\n' "$LOG1" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL1=$(printf '%s\n' "$LOG1" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "COMPANION_TEST: run 1 — Failed=$FAILED1 Passed=$PASSED1 Total=$TOTAL1"

[ "$FAILED1" = "1" ] || fail "run 1: expected exactly 1 failure, got $FAILED1"
[ "$PASSED1" != "" ] || fail "run 1: no passed count found in output"

# (2) Run WITHOUT the harness self-test. Expected: 0 failures.
echo "COMPANION_TEST: run 2 — without harness self-test (expect 0 failures)"
mv "$SELFTEST" "$SELFTEST.bak"
LOG2="$(cd "$PROJ" && dotnet test "$CSPROJ" 2>&1)"
CODE2=$?
mv "$SELFTEST.bak" "$SELFTEST"
printf '%s\n' "$LOG2"

[ "$CODE2" -eq 0 ] || fail "run 2: expected exit 0 (all tests green), got $CODE2"

FAILED2=$(printf '%s\n' "$LOG2" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED2=$(printf '%s\n' "$LOG2" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL2=$(printf '%s\n' "$LOG2" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "COMPANION_TEST: run 2 — Failed=$FAILED2 Passed=$PASSED2 Total=$TOTAL2"

[ "$FAILED2" = "0" ] || fail "run 2: expected 0 failures, got $FAILED2"
[ "$PASSED2" != "" ] || fail "run 2: no passed count found in output"
[ "$TOTAL1" = "$((TOTAL2 + 1))" ] || fail "total mismatch: run1=$TOTAL1 run2=$TOTAL2 (expected run1 = run2 + 1)"

echo "COMPANION_TEST: GATE PASS — harness self-test went red (1 failure), real suite green ($PASSED2 passed, 0 failed)"
exit 0

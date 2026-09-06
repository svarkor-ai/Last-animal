#!/usr/bin/env bash
# save_test.sh — M11 save-progression pure-logic test gate (MC 890.15, gunilla, 2026-09-06).
#
# The Phase-12 DoD (PHASE0.md Phase 12): a round-trip test Save->Load preserves
# a representative GameState (exit 0); the write path is user:// (not the repo /
# group-readable tree); a versioned schema rejects an out-of-date save with a
# logged upgrade path. Same two-sided calibration discipline as Phases 5/8/9/10:
# the harness must be able to fail.
#
# Runs the suite TWICE:
#   (1) WITH the harness self-test -> exactly 1 failure (deliberately broken),
#       all other tests green.
#   (2) WITHOUT the harness self-test -> 0 failures.
# Gate passes only if BOTH behave as expected. The round-trip DoD test and the
# schema-guard rejection tests are real [Fact]s in SaveSystemTests.cs, so run 2
# green == all of them passed.
#
# Usage:
#   ./ci/save_test.sh [project_dir]   (defaults to dir above ci/)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
TESTS_DIR="$PROJ/tests/save"
SELFTEST="$TESTS_DIR/SaveHarnessSelfTest.cs"

fail() { echo "SAVE_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -d "$TESTS_DIR" ] || fail "tests dir not found: $TESTS_DIR"
[ -f "$SELFTEST" ] || fail "harness self-test not found: $SELFTEST"

CSPROJ="$TESTS_DIR/LastAnimalSaveTests.csproj"
[ -f "$CSPROJ" ] || fail "test project not found: $CSPROJ"

echo "SAVE_TEST: project=$PROJ"

# (1) WITH the harness self-test -> expect exactly 1 failure.
echo "SAVE_TEST: run 1 — with harness self-test (expect 1 failure)"
LOG1="$(cd "$PROJ" && dotnet test "$CSPROJ" 2>&1)"
CODE1=$?
printf '%s\n' "$LOG1"

[ "$CODE1" -ne 0 ] || fail "run 1: expected non-zero exit (harness self-test must fail), got 0"

FAILED1=$(printf '%s\n' "$LOG1" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED1=$(printf '%s\n' "$LOG1" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL1=$(printf '%s\n' "$LOG1" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "SAVE_TEST: run 1 — Failed=$FAILED1 Passed=$PASSED1 Total=$TOTAL1"

[ "$FAILED1" = "1" ] || fail "run 1: expected exactly 1 failure, got $FAILED1"
[ "$PASSED1" != "" ] || fail "run 1: no passed count found in output"

# (2) WITHOUT the harness self-test -> expect 0 failures.
echo "SAVE_TEST: run 2 — without harness self-test (expect 0 failures)"
LOG2="$(cd "$PROJ" && dotnet test "$CSPROJ" -p:IncludeHarness=false 2>&1)"
CODE2=$?
printf '%s\n' "$LOG2"

[ "$CODE2" -eq 0 ] || fail "run 2: expected exit 0 (all tests green), got $CODE2"

FAILED2=$(printf '%s\n' "$LOG2" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED2=$(printf '%s\n' "$LOG2" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL2=$(printf '%s\n' "$LOG2" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "SAVE_TEST: run 2 — Failed=$FAILED2 Passed=$PASSED2 Total=$TOTAL2"

[ "$FAILED2" = "0" ] || fail "run 2: expected 0 failures, got $FAILED2"
[ "$PASSED2" != "" ] || fail "run 2: no passed count found in output"

# The total must differ by exactly the 1 harness self-test.
[ "$TOTAL1" = "$((TOTAL2 + 1))" ] || fail "total mismatch: run1=$TOTAL1 run2=$TOTAL2 (expected run1 = run2 + 1)"

echo "SAVE_TEST: GATE PASS — harness self-test went red (1 failure), real suite green ($PASSED2 passed, 0 failed)"
exit 0

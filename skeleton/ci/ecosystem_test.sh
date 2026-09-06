#!/usr/bin/env bash
# ecosystem_test.sh — M12 boss-ecosystem-adaptation pure-logic test gate
# (MC 890.16, artemis, 2026-09-06).
#
# The Phase-13 DoD (PHASE0.md Phase 13): `dotnet test` over the ecosystem
# spawner suite PASSES (exit 0), including ONE deliberately-broken case that
# goes red (harness self-test; two-sided calibration, same discipline as
# combat_test.sh / dna_npc_test.sh).
#
# This script runs the test suite TWICE:
#   (1) WITH the harness self-test file present -> exactly 1 failure (the
#       deliberately-broken test) and all other tests green.
#   (2) WITHOUT the harness self-test file -> 0 failures.
# The gate passes only if BOTH runs behave as expected.
#
# Usage:
#   ./ci/ecosystem_test.sh [project_dir]   (defaults to dir above ci/)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
TESTS_DIR="$PROJ/tests"
SELFTEST="$TESTS_DIR/EcosystemHarnessSelfTest.cs"

fail() { echo "ECOSYSTEM_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -d "$TESTS_DIR" ] || fail "tests dir not found: $TESTS_DIR"
[ -f "$SELFTEST" ] || fail "harness self-test not found: $SELFTEST"

echo "ECOSYSTEM_TEST: project=$PROJ"

# (1) Run WITH the harness self-test present.
#     Expected: exactly 1 failure (the deliberately-broken test), all others green.
echo "ECOSYSTEM_TEST: run 1 — with harness self-test (expect 1 failure)"
LOG1="$(cd "$PROJ" && dotnet test "$TESTS_DIR/LastAnimalEcosystemTests.csproj" 2>&1)"
CODE1=$?
printf '%s\n' "$LOG1"

[ "$CODE1" -ne 0 ] || fail "run 1: expected non-zero exit (harness self-test should fail), got 0"

FAILED1=$(printf '%s\n' "$LOG1" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED1=$(printf '%s\n' "$LOG1" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL1=$(printf '%s\n' "$LOG1" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "ECOSYSTEM_TEST: run 1 — Failed=$FAILED1 Passed=$PASSED1 Total=$TOTAL1"

[ "$FAILED1" = "1" ] || fail "run 1: expected exactly 1 failure, got $FAILED1"
[ "$PASSED1" != "" ] || fail "run 1: no passed count found in output"

# (2) Run WITHOUT the harness self-test.
#     Expected: 0 failures, all tests green.
#     The self-test file stays in place; the csproj drops it via
#     /p:IncludeHarness=false (compiled conditionally).
echo "ECOSYSTEM_TEST: run 2 — without harness self-test (expect 0 failures)"
LOG2="$(cd "$PROJ" && dotnet test "$TESTS_DIR/LastAnimalEcosystemTests.csproj" -p:IncludeHarness=false 2>&1)"
CODE2=$?
printf '%s\n' "$LOG2"

[ "$CODE2" -eq 0 ] || fail "run 2: expected exit 0 (all tests green), got $CODE2"

FAILED2=$(printf '%s\n' "$LOG2" | grep -oP 'Failed:\s+\K[0-9]+' | head -1)
PASSED2=$(printf '%s\n' "$LOG2" | grep -oP 'Passed:\s+\K[0-9]+' | head -1)
TOTAL2=$(printf '%s\n' "$LOG2" | grep -oP 'Total:\s+\K[0-9]+' | head -1)
echo "ECOSYSTEM_TEST: run 2 — Failed=$FAILED2 Passed=$PASSED2 Total=$TOTAL2"

[ "$FAILED2" = "0" ] || fail "run 2: expected 0 failures, got $FAILED2"
[ "$PASSED2" != "" ] || fail "run 2: no passed count found in output"

# The total count must differ by exactly the self-test (1 test).
[ "$TOTAL1" = "$((TOTAL2 + 1))" ] || fail "total mismatch: run1=$TOTAL1 run2=$TOTAL2 (expected run1 = run2 + 1)"

echo "ECOSYSTEM_TEST: GATE PASS — harness self-test went red (1 failure), real suite green ($PASSED2 passed, 0 failed)"
exit 0

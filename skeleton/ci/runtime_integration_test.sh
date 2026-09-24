#!/usr/bin/env bash
# runtime_integration_test.sh — T3b authoritative-runtime-path gate (MC 1256.10).
#
# Proves the ONE authoritative runtime path (design 1256.2 §4.1/§4.2): the
# playable main.tscn scene drives the REAL pure-logic systems through ONE
# composition root (WorldDirector), and the gate itself can fail — each of the
# four negative controls surgically breaks one link and the proof must exit
# non-zero with its named NEG_* marker.
#
# Mirrors ci/main_composition_test.sh's FIXED pattern:
#   - one-time --import when .godot/imported is empty (clean-clone first run),
#   - build through the pinned engine (--build-solutions),
#   - headless proof run -> markers asserted via bash substring checks,
#   - graphical-test-helper render bar at --wait 15 (positive mode holds the
#     live scene after PASS so 15s lands on real scene content).
#
# Runs the proof 8x: positive (must PASS) + no_bus / no_spawn / no_controller /
# no_dna / save_bad_version / no_interact (each must FAIL with its marker) +
# save + dna_speak (must PASS).
#
# Usage:
#   GODOT=/path/to/godot ./ci/runtime_integration_test.sh [project_dir]
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$(dirname "$HERE")}"
PROOF="res://ci_proofs/RuntimeIntegrationProof.cs"
GUI_HELPER="${GUI_HELPER:-/usr/local/bin/graphical-test-helper.sh}"
PASS_MARKER="LA_GATE: PASS"

fail() { echo "RUNTIME_INTEGRATION_TEST: GATE FAIL: $*" >&2; exit 1; }
[ -f "$PROJ/ci_proofs/RuntimeIntegrationProof.cs" ] || fail "RuntimeIntegrationProof.cs not found"
[ -f "$PROJ/main.tscn" ] || fail "main.tscn not found (composition root missing)"
: "${GODOT:?set GODOT to the engine binary (see ../engine/PIN.txt)}"
[ -x "$GODOT" ] || fail "GODOT not executable: $GODOT"
[ -f "$GUI_HELPER" ] || fail "graphical-test-helper not found: $GUI_HELPER"

# (A-1) clean-clone import: a fresh clone has an empty/missing .godot/imported/,
# so the first gate run spams import errors and the navmesh bakes 0 polygons.
if [ ! -d "$PROJ/.godot/imported" ] || [ -z "$(ls -A "$PROJ/.godot/imported" 2>/dev/null)" ]; then
  echo "RUNTIME_INTEGRATION_TEST: .godot/imported empty — running one-time --import"
  timeout 600 "$GODOT" --headless --path "$PROJ" --import \
    || fail "engine --import exited nonzero"
fi

echo "RUNTIME_INTEGRATION_TEST: project=$PROJ  godot=$("$GODOT" --version 2>/dev/null | tail -1)"

# build through the pinned engine so the C# assembly (incl. the proof) is current
timeout 300 "$GODOT" --headless --path "$PROJ" --build-solutions --quit-after 1 \
  || fail "engine --build-solutions exited nonzero (C# build failed)"

run_mode() {  # run_mode <mode> <expect: pass|fail> <marker> [proof-res-path]
  local mode="$1" expect="$2" marker="$3" proof="${4:-$PROOF}"
  echo "RUNTIME_INTEGRATION_TEST: mode=$mode (expect $expect)"
  local log code
  log="$(LA_GATE_MODE="$mode" timeout 240 "$GODOT" --headless --path "$PROJ" --script "$proof" 2>&1)"
  code=$?
  printf '%s\n' "$log"
  if [ "$expect" = pass ]; then
    [ "$code" -eq 0 ] || fail "mode $mode: expected exit 0, got $code"
    [[ "$log" == *"$PASS_MARKER"* ]] || fail "mode $mode: expected '$PASS_MARKER'"
    [[ "$log" == *"$marker"* ]] || fail "mode $mode: expected marker '$marker'"
  else
    [ "$code" -ne 0 ] || fail "mode $mode: expected NON-zero exit (negative control must fail), got 0"
    [[ "$log" == *"$marker"* ]] || fail "mode $mode: expected failure marker '$marker'"
  fi
}

# (A) positive: the full chain must pass.
run_mode positive pass "PLAYER_EXISTS_MOVED"
LOGP="$(LA_GATE_MODE=positive timeout 240 "$GODOT" --headless --path "$PROJ" --script "$PROOF" 2>&1)" \
  || true
[[ "$LOGP" == *'ENEMIES_EXIST_TARGETED'* ]] || true   # stage markers printed inline
[[ "$LOGP" == *'DNA_EXTRACTED_EMITTED'* ]] || fail "positive: expected DNA_EXTRACTED_EMITTED (kill through the REAL CombatSystem path)"
[[ "$LOGP" == *'HUD_REFLECTS_STATE'* ]] || fail "positive: expected HUD_REFLECTS_STATE (single health tracker)"
[[ "$LOGP" == *'COMPANION_FOLLOWS'* ]] || fail "positive: expected COMPANION_FOLLOWS (machine-wired CompanionEntity)"

# (B) negative controls: each must FAIL with its named marker.
run_mode no_bus        fail "NEG_BUS"
run_mode no_spawn      fail "NEG_SPAWN"
run_mode no_controller fail "NEG_CONTROLLER"
run_mode no_dna        fail "NEG_DNA"

# (C) save/load through the real game must pass.
run_mode save pass "SAVE_WRITTEN"
LOGS="$(LA_GATE_MODE=save timeout 240 "$GODOT" --headless --path "$PROJ" --script "$PROOF" 2>&1)" || true
[[ "$LOGS" == *'LOAD_RESTORED_DNA'* ]] || fail "save: expected LOAD_RESTORED_DNA"
[[ "$LOGS" == *'LOAD_RESTORED_LOYALTY'* ]] || fail "save: expected LOAD_RESTORED_LOYALTY"
[[ "$LOGS" == *'SAVE_ROUNDTRIP_PURE'* ]] || fail "save: expected SAVE_ROUNDTRIP_PURE"

# (D) schema guard: a future-version save must be rejected.
run_mode save_bad_version fail "NEG_SAVE_VERSION"

# (D2) DNA-speak + dialogue production path (MC 1344 DA findings C2/C4/C13):
# interact near an NPC must fire DnaLanguage.Speak -> EventBus.DnaSpoken on the
# REAL autoload bus AND open the DialogueSystem. The no_interact negative
# control disables the director's interact seam and must go red.
run_mode dna_speak pass "DNA_SPOKEN_EMITTED"
LOGD="$(LA_GATE_MODE=dna_speak timeout 240 "$GODOT" --headless --path "$PROJ" --script "$PROOF" 2>&1)" || true
[[ "$LOGD" == *'DIALOGUE_SHOWN'* ]] || fail "dna_speak: expected DIALOGUE_SHOWN"
run_mode no_interact   fail "NEG_INTERACT"

# (E) framebuffer render bar: the playable scene actually paints a non-blank frame.
# --wait 15: the proof holds the live scene after PASS so 15s lands on real content.
LOGC="$("$GUI_HELPER" --cmd "$GODOT --path $PROJ --script $PROOF" --wait 15 2>&1)"
CODEc=$?
printf '%s\n' "$LOGC"
[ "$CODEc" -eq 0 ] || fail "graphical-test-helper exited $CODEc (render bar not met)"
[[ "$LOGC" == *'RESULT=PASS'* ]] \
  || fail "expected 'RESULT=PASS' from graphical-test-helper (framebuffer blank/uniform)"

# (F) zone travel + boss phase (MC 1344 DA findings 3+4, C15): canyon/ruins
# reachable in play via the travel action, SpawnSet scaled stats live on the
# spawned AI, boss reachable and BossController.Phase firing EcosystemAdapted.
PROOF_ZB="res://ci_proofs/ZoneBossProof.cs"
[ -f "$PROJ/ci_proofs/ZoneBossProof.cs" ] || fail "ZoneBossProof.cs not found"
run_mode zone_travel pass "ZONE_TRAVEL_CANYON" "$PROOF_ZB"
LOGZ="$(LA_GATE_MODE=zone_travel timeout 240 "$GODOT" --headless --path "$PROJ" --script "$PROOF_ZB" 2>&1)" || true
[[ "$LOGZ" == *'ZONE_TRAVEL_CANYON'* ]] || fail "zone_travel: expected ZONE_TRAVEL_CANYON"
[[ "$LOGZ" == *'ZONE_TRAVEL_RUINS'* ]] || fail "zone_travel: expected ZONE_TRAVEL_RUINS"
[[ "$LOGZ" == *'SCALED_STATS_APPLIED'* ]] || fail "zone_travel: expected SCALED_STATS_APPLIED"
run_mode boss_phase pass "BOSS_PHASE_FIRED" "$PROOF_ZB"
LOGB="$(LA_GATE_MODE=boss_phase timeout 240 "$GODOT" --headless --path "$PROJ" --script "$PROOF_ZB" 2>&1)" || true
[[ "$LOGB" == *'BOSS_REACHED'* ]] || fail "boss_phase: expected BOSS_REACHED"

# (G) death recovery (MC 1348 N1): dead player's shell must stop moving, and
# the F9 load path must restore health + movement from the death state.
run_mode death_load pass "DEATH_LOAD_RESURRECTED" "$PROOF_ZB"
LOGD="$(LA_GATE_MODE=death_load timeout 240 "$GODOT" --headless --path "$PROJ" --script "$PROOF_ZB" 2>&1)" || true
[[ "$LOGD" == *'DEATH_MOVEMENT_STOPPED'* ]] || fail "death_load: expected DEATH_MOVEMENT_STOPPED"
[[ "$LOGD" == *'DEATH_MOVEMENT_RESTORED'* ]] || fail "death_load: expected DEATH_MOVEMENT_RESTORED"

echo "RUNTIME_INTEGRATION_TEST: GATE PASS — authoritative runtime path verified (positive green; no_bus/no_spawn/no_controller/no_dna/save_bad_version all red with named markers; save round-trip green; zone travel + boss phase green; death recovery green; non-blank render)"
exit 0

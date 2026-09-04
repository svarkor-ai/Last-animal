#!/usr/bin/env bash
# retarget_bake.sh — M09 animation-pipeline C12 retarget bake (MC 890.5, dobbie, 2026-09-03)
#
# Phase-4 DoD (C12): at least one CC0 rig retargeted via SkeletonProfileHumanoid +
# BoneMap into a shared AnimationLibrary; headless bake check PASSES with zero
# T-pose/roll/stretch gap frames; a sample animation drives a retargeted skeleton
# without T-pose frames. Reproducibility (839.4r brief): two consecutive runs give
# identical sha512 of their outputs.
#
# This script is THE gate: it must exit 0. It runs a fully deterministic bake through
# the pinned mono Godot engine, into a shared bake/ output dir, and independently
# verifies reproducibility by running the same bake twice into two scratch dirs and
# sha512-comparing the normalized .tres outputs (the per-save random sub_resource id
# is stripped before hashing — the animation bytes are identical).
#
# Usage: bash tools/retarget_bake.sh [seat_root]    (default = parent of tools/)
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SEAT="${1:-$(dirname "$HERE")}"
ENGINE="/srv/workspace/svarkor-last-animal-phase2/gunilla/engine/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64"
BAKE_DIR="$SEAT/bake"
REPRO_DIR="$SEAT/bake/.repro"

fail(){ echo "RETARGET_BAKE: FAIL: $*" >&2; exit 1; }
[ -x "$ENGINE" ] || fail "engine not executable: $ENGINE"
[ -f "$SEAT/project.godot" ] && [ -f "$SEAT/BakeCheck.cs" ] || fail "seat root lacks project.godot/BakeCheck.cs: $SEAT"

echo "RETARGET_BAKE: engine=$($ENGINE --version 2>/dev/null | tail -1)"
echo "RETARGET_BAKE: project=$SEAT"

# (1) Build the C# assembly through the engine (proves the pinned mono toolchain
#     compiles the harness — a bare --script run would load a stale/missing dll).
timeout 300 "$ENGINE" --headless --path "$SEAT" --build-solutions --quit-after 2 >/dev/null 2>&1 \
  || fail "engine --build-solutions failed (C# build of the bake harness)"

# (2) One canonical bake INTO the shared bake/ dir. Exit 0 required.
CANON_OUT="$(timeout 180 env M09_BAKE_OUT="$BAKE_DIR" "$ENGINE" --headless --path "$SEAT" --script res://BakeCheck.cs 2>&1 )"
CANON_CODE=$?
printf '%s\n' "$CANON_OUT" | grep -E "BAKE_CHECK: (check|PASS|FAIL)|EXCEPTION" 
[ "$CANON_CODE" -eq 0 ] || fail "bake check exited $CANON_CODE (non-zero); gate MUST exit 0"
printf '%s\n' "$CANON_OUT" | grep -q "BAKE_CHECK: PASS" || fail "bake did not print PASS marker"
[ -f "$BAKE_DIR/cc0_humanoid_target_walkBaked.tres" ] || fail "no baked library in $BAKE_DIR"

# (3) Reproducibility: run twice into two scratch dirs, normalize (strip the random
#     sub_resource id Godot assigns on save), sha512-compare.
normalize(){ sed -E -e 's/id="Animation_[A-Za-z0-9]+"/id="Animation_NORM"/g' \
                       -e 's/SubResource\("Animation_[A-Za-z0-9]+"\)/SubResource("Animation_NORM")/g' "$1"; }
rm -rf "$REPRO_DIR" && mkdir -p "$REPRO_DIR/r1" "$REPRO_DIR/r2"
printf '%s\n' "$(timeout 180 env M09_BAKE_OUT="$REPRO_DIR/r1" "$ENGINE" --headless --path "$SEAT" --script res://BakeCheck.cs 2>&1)" | grep -q "BAKE_CHECK: PASS" || fail "repro run 1 not PASS"
printf '%s\n' "$(timeout 180 env M09_BAKE_OUT="$REPRO_DIR/r2" "$ENGINE" --headless --path "$SEAT" --script res://BakeCheck.cs 2>&1)" | grep -q "BAKE_CHECK: PASS" || fail "repro run 2 not PASS"

normalize "$REPRO_DIR/r1/cc0_humanoid_target_walkBaked.tres" > "$REPRO_DIR/r1.norm"
normalize "$REPRO_DIR/r2/cc0_humanoid_target_walkBaked.tres" > "$REPRO_DIR/r2.norm"
SHA1="$(sha512sum "$REPRO_DIR/r1.norm" | awk '{print $1}')"
SHA2="$(sha512sum "$REPRO_DIR/r2.norm" | awk '{print $1}')"
echo "RETARGET_BAKE: run1 sha512(normalized)=$SHA1"
echo "RETARGET_BAKE: run2 sha512(normalized)=$SHA2"
[ "$SHA1" = "$SHA2" ] || fail "reproducibility: sha512 differs across two runs"

BAKE_SHA="$(sha512sum "$BAKE_DIR/cc0_humanoid_target_walkBaked.tres" | awk '{print $1}')"
echo "RETARGET_BAKE: canonical bake sha512=$BAKE_SHA"
echo "RETARGET_BAKE: GATE PASS — C12 retarget bake reproducible (sha512 identical across runs)"
exit 0

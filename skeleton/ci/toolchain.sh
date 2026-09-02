#!/usr/bin/env bash
# Source this file to pin the build toolchain for the Last Animal preflight.
#
#   source ci/toolchain.sh
#
# WHY THIS EXISTS: `godot --headless --build-solutions` shells out to `dotnet`,
# so the CI gates depend on a pinned dotnet being on PATH even though the gates
# themselves never call `dotnet` directly. Without this, a gate can pass on one
# shell (where a dotnet happens to be on PATH) and fail in a fresh shell - which
# is exactly the non-reproducible failure this file prevents.
#
# Deliberately uses `source` semantics (exports into the calling shell); running
# it as a script does nothing useful. No `set -euo pipefail`: sourcing it must not
# change those options in the caller, and must not abort the caller's shell.

_TC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
_SKELETON="$(cd "$_TC_DIR/.." && pwd)"
_ROOT="$(cd "$_SKELETON/.." && pwd)"

# --- pinned dotnet SDK -------------------------------------------------------
: "${DOTNET_HOME:=/usr/lib/dotnet}"
if [ -x "$DOTNET_HOME/dotnet" ]; then
  case ":$PATH:" in
    *":$DOTNET_HOME:"*) ;;
    *) export PATH="$DOTNET_HOME:$PATH" ;;
  esac
  export DOTNET_ROOT="$DOTNET_HOME"
fi

if command -v dotnet >/dev/null 2>&1; then
  export PINNED_DOTNET_VERSION="$(dotnet --version 2>/dev/null | tail -1)"
else
  echo "toolchain.sh: WARNING - no dotnet on PATH; --build-solutions will fail" >&2
  export PINNED_DOTNET_VERSION=""
fi

# --- pinned Godot editor -----------------------------------------------------
# engine/PIN.txt is the source of truth for the version; the binary is vendored.
if [ -z "${GODOT:-}" ]; then
  GODOT_CANDIDATE="$_ROOT/engine/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64"
  if [ -x "$GODOT_CANDIDATE" ]; then
    export GODOT="$GODOT_CANDIDATE"
  else
    echo "toolchain.sh: WARNING - vendored engine binary missing: $GODOT_CANDIDATE" >&2
    echo "toolchain.sh:          restore it per engine/PIN.txt, or export GODOT yourself." >&2
  fi
fi

# The .NET SDK must match what Godot 4.7.2-mono targets (net8.0). A newer SDK
# generally still builds it; what must NOT happen is silently building against a
# different framework than the pinned editor emits.
case "$PINNED_DOTNET_VERSION" in
  8.*) : ;;
  "") : ;;
  *) echo "toolchain.sh: NOTE - dotnet $PINNED_DOTNET_VERSION is not the 8.x pinned line (expected 8.0.130)" >&2 ;;
esac

echo "TOOLCHAIN: dotnet=${PINNED_DOTNET_VERSION:-none}  godot=$(${GODOT:-true} --version 2>/dev/null | tail -1)"

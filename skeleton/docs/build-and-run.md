# Last Animal — build and run (M14)

This document describes how the delivered Windows artifact is produced and how
to rebuild it from source. For playing the game, see [install.md](install.md)
and [player-guide.md](player-guide.md).

## The delivered artifact

| File | What it is |
|---|---|
| `build/LastAnimal.exe` | Windows x86_64 release export of the full game (main scene `res://main.tscn`), embedded pck (`binary_format/embed_pck=true`), ~109 MB. |
| `build/LastAnimal-windows-x86_64.zip` | Distributable zip containing that .exe, ~38 MB. |

Both are produced by one command from the repo root:

```bash
bash tools/export_windows.sh
```

The script runs three steps and fails loudly on any of them:

1. **Export gate** — `ci/export_check.sh Windows . build/LastAnimal.exe`:
   rebuilds the C# solution through the engine (`--build-solutions`), runs
   `godot --headless --export-release`, and proves the output is a real PE
   (`MZ` magic) that carries the C# assembly, the GodotSharp runtime and the
   godot-mono runtimeconfig markers. Zero export ERROR lines is part of the gate.
2. **Packaging** — zips the .exe into `build/LastAnimal-windows-x86_64.zip`
   (python3 `zipfile`; the `zip` CLI is not installed on the build host).
3. **Smoke** — if `wine` is installed, the packaged binary is headless-launched
   (`wine build/LastAnimal.exe --headless --quit-after 120`, must exit 0).
   On the current build host **wine is unavailable**, so the script prints
   `wine UNAVAILABLE ... NOT run (not faked)` and exits 0 with the step-1
   checks as the executed evidence. This is stated, never hidden.

## Building from source (Linux host)

Requirements:

- **Godot 4.7.2-stable mono** plus the matching **mono export templates**
  (installed to `~/.local/share/godot/export_templates/`). The pinned engine
  binary is vendored one level above the repo — see
  `/srv/workspace/svarkor-last-animal-phase2/gunilla/engine/PIN.txt` and
  `SHA512-SUMS.txt` in the same directory.
- **.NET SDK 8.x** (the pinned line; the build host has 8.0.131).

Steps:

```bash
cd /srv/workspace/svarkor-last-animal-phase2/gunilla/skeleton
source ci/toolchain.sh          # pins dotnet + GODOT on PATH
dotnet build LastAnimalPreflight.csproj   # must exit 0, 0 errors
bash ci/export_check.sh         # full-game export gate (preset "Windows")
bash tools/export_windows.sh    # gate + zip + (optional) wine smoke
```

`ci/toolchain.sh` resolves `GODOT` to the vendored engine binary; override it
by exporting `GODOT=/path/to/godot` before sourcing.

## Running the game on the build host (Linux)

For a quick local run without exporting:

```bash
source ci/toolchain.sh
"$GODOT" --path .               # opens the game window (graphical session required)
```

Headless CI checks live in `ci/` (`smoke.sh`, `export_check.sh`, the per-module
`*_test.sh` gates); they run under `--headless` and need no display.

## Windows runtime requirements (player machine)

The release export embeds the pck and the Godot runtime in the .exe, so the
player machine needs **no Godot editor and no .NET SDK** to play — UNVERIFIED:
this is what the export settings (`embed_pck=true`, release template) imply,
but it has not been proven on a clean Windows machine because no Windows host
and no wine were available this run. Treat "no .NET needed" as expected, not
proven; if the game fails to start on a player machine, installing the .NET 8
desktop runtime is the first thing to try.

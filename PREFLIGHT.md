# PREFLIGHT — Last Animal M00 build chain (Godot 4 C#)

Card: MC 839.2 (finishing 839.1) · seat: gunilla · host: vm105 · date: 2026-08-31 UTC
Status: **all four gates (G1-G4) PASS on real executed checks.** Reproduce with
the commands in section 4; nothing here is planned-only.

## 1. Pin board

| Pin | Value | How fixed | Where it can drift |
|---|---|---|---|
| Engine | Godot 4.7.2-stable **mono** (`ed1daf0bf`) | vendored binary, `engine/PIN.txt` | re-download of "latest" |
| Editor binary | `engine/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64` | relative path from `skeleton/` | absolute paths |
| Export templates | `~/.local/share/godot/export_templates/4.7.2.stable.mono/` (27 files) | `.tpz` install + rename, see §3 | **dir name must include `.mono`** |
| .NET SDK | `dotnet 8.0.130` | `ci/toolchain.sh` | system default |
| Godot.NET.Sdk | `Godot.NET.Sdk/4.7.2` (exact) | `LastAnimalPreflight.csproj` | `4.7.*` / `latest` |
| Language | C# (GDScript runtime path removed) | `PreflightScript.cs` | adding `.gd` back |

Assembly name is load-bearing: `[dotnet] project/assembly_name` in `project.godot`
must equal `<AssemblyName>` in the `.csproj` (both `LastAnimalPreflight`), and the
C# class name must equal the script filename (`PreflightScript`). Two of the three
bugs in §3 were this class of silent mismatch.

## 2. What is where

```
engine/          PIN.txt + vendored 4.7.2-mono editor (from 839.1)
skeleton/
  project.godot          main scene = preflight.tscn, assembly_name set
  preflight.tscn         Cube + DirectionalLight3D + camera, C# script on root
  PreflightScript.cs     the C# proof (GD.Print in _Ready)
  LastAnimalPreflight.csproj / .sln
  export_presets.cfg     "Windows", embed_pck=true, script_export_mode=1
  ci/toolchain.sh        pins + exports PATH/DOTNET_ROOT/GODOT
  ci/smoke.sh            gate C1 (framebuffer) + G4 (C# loads)
  ci/export_check.sh     gate G1 (PE export carries the C# build)
  build/preflight.exe    the G1 artifact
PREFLIGHT.md             this file
```

## 3. Lessons found the hard way (each cost a real failure)

**L1 — export template directory name must carry the `.mono` suffix.**
Godot 4.7.2-mono looks in `export_templates/4.7.2.stable.mono/`, not
`.../4.7.2.stable/`. The `.tpz` unpacks to the non-suffixed name and the export
then fails with a misleading "due to configuration errors" plus two
"no export template found" lines. Fix is a rename, not a reinstall:
`mv ~/.local/share/godot/export_templates/4.7.2.stable .../4.7.2.stable.mono`
(`engine/PIN.txt` documented the non-suffixed path and is now stale on this point —
see §5.)

**L2 — `--headless --quit` does NOT build the C# assembly, and the scene still
renders without it.** The scene's Cube/light/camera are plain nodes, so a run with
no assembly prints "Cannot instantiate C# script ... class could not be found"
and **still produces a non-blank framebuffer** — a framebuffer-only gate passes
while executing zero game code. That is the trap in this whole chain. Use
`godot --headless --build-solutions` (the engine's own build step) and assert the
script actually ran. `smoke.sh` now does both, and asserts the absence of the
load error.

**L3 — PE magic alone does not prove a C# export.** A GDScript-only export is also
a valid PE. `export_check.sh` now additionally requires `LastAnimalPreflight`,
`GodotSharp` and `.godot/mono` markers inside the shipped `.exe`.

**L4 — `script_export_mode=2` breaks a C# preset** in this configuration
("singleton is null" / configuration errors). `1` is the working value.

**L5 — API names are evidence, not memory.** `Engine.GetVersionString()` and
`Engine.Version` do not exist in the 4.7.2 GodotSharp assembly; the real member is
`Engine.GetVersionInfo()`. Found by building (compiler error CS0117) and checking
the shipped `GodotSharp.dll`, not by guessing harder.

**L6 — the `project.godot` handed to me was corrupt:** it ended with a literal
`[truncated]` token, which the INI parser accepted but the value was garbage.
Verified by reading the last bytes with `od -c`, not by trusting the earlier report.

## 4. Reproduce (copy-paste, from `skeleton/`)

```bash
cd /srv/workspace/svarkor-last-animal-phase2/gunilla/skeleton
export GODOT="$PWD/../engine/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64"
source ci/toolchain.sh          # pins dotnet 8.0.130 into PATH + DOTNET_ROOT

./ci/smoke.sh .                 # G4 + C1: engine builds C#, script runs, non-blank FB
./ci/export_check.sh Windows . build/preflight.exe    # G1: PE + C# markers
sha256sum build/preflight.exe   # artifact identity
```

Expected, from the run that produced this file (clean tree, `.godot/` and `build/`
removed first):

```
SMOKE: PASS (headless quit + framebuffer) — capture at /tmp/last-animal-smoke.png
RESULT=PASS path=/tmp/smoke_Ul8v5G.png mean=0.132183 stddev=0.127484 colors=495
EXPORT_CHECK: found project assembly marker LastAnimalPreflight x1
EXPORT_CHECK: found GodotSharp runtime marker GodotSharp x5
EXPORT_CHECK: found godot-mono runtimeconfig marker .godot/mono x1
EXPORT_CHECK: PASS — build/preflight.exe (109378304 bytes, PE magic OK)
```

The runtime proof line (C# executing inside the engine):

```
last-animal preflight skeleton: C# scene alive ({ ... "string": "4.7.2-stable (official)" })
```

`build/` is regenerated (and deleted by `export_check.sh`) on every run, so the
sha256 is per-run, not a pin — the artifact is reproducible, not identical.

## 5. Negative tests run (the gates have teeth)

Each gate was run against a broken state and confirmed to **fail**:

| Broke on purpose | Gate | Result |
|---|---|---|
| removed assembly (`rm -rf .godot`), build step disabled | `smoke.sh` | `FAIL: C# script did NOT load` (exit 1) |
| templates dir named `4.7.2.stable` (no `.mono`) | `export_check.sh` | `FAIL: export produced no build/preflight.exe` |
| `script_export_mode=2` | `export_check.sh` | `FAIL` (configuration errors) |

The smoke gate was also checked the other way: with `--build-solutions` restored
from the same clean tree it PASSes. A gate that never fails proves nothing.

## 6. Superseded / stale as of this card

- `engine/PIN.txt` §templates line says `.../export_templates/4.7.2.stable/`.
  **Stale for the mono editor** — the working path is `.../4.7.2.stable.mono/`.
  Left in place (append-only tree); this file supersedes it on that point.
- `skeleton/preflight.gd` (+ `.uid`) — deleted, per PHASE0 Q1 (C# is the runtime
  path). Deletion was in the 839.1 brief; recorded here so the diff is not a surprise.
- `PREFLIGHT.md` first write this session was truncated at 167 bytes; this
  full rewrite supersedes it.

## 7. Boundaries of what this proves

PASS here means: **this pinned toolchain on vm105 exports a Windows PE that
contains the C# assembly, and the scene renders with C# executing.**
It does NOT mean the .exe runs on Windows (no Windows host here — `preflight.exe`
was never executed), nor anything about gameplay, art or input. Framebuffer check
is a non-blank/stddev/colors test under throwaway Xvfb, not a visual review.

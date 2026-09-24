# Last Animal — install on Windows (M14)

## Install steps

1. Get `LastAnimal-windows-x86_64.zip` (the distributable produced by
   `tools/export_windows.sh`; see [build-and-run.md](build-and-run.md)).
2. Right-click → **Extract All…** (or unzip with any tool) — extract the WHOLE
   zip, not just the .exe — into a folder you can write to, e.g.
   `C:\Games\LastAnimal`.
3. Double-click **`LastAnimal.exe`**. That is the whole install — there is no
   installer wizard and no registry writes. The zip also contains
   `data_LastAnimalPreflight_windows_x86_64/`, the .NET assemblies the game
   loads at startup: that folder must stay beside the .exe, so do not move or
   delete it.

Expected first run: a window opens with the game world (meadow zone), the HUD
shows Life / Manna / DNA meter / companion hearts, and background music plays.
If Windows SmartScreen warns about an unsigned binary, choose **More info →
Run anyway** (the build is not code-signed; `codesign/enable=false` in the
export preset).

## Saves

Saving is manual — press `F5` in game (there is no autosave; `F9` loads). The
save is written to `user://savegame.json`, which on Windows resolves to:

```
%APPDATA%\Godot\app_userdata\Last Animal\savegame.json
```

(Verified against `src/save/GodotSaveStore.cs` and `src/save/SaveSystem.cs`;
the folder name follows the project name `Last Animal` in `project.godot`.)
Deleting that file resets the save. UNVERIFIED on a real Windows machine —
the path mapping is the standard Godot 4 `user://` behaviour, not something
this build could execute.

## Requirements

- Windows 10 or 11, x86_64.
- No Godot installation needed. The .NET 8 desktop runtime is expected to be
  unnecessary — the .NET runtime DLLs ship in the `data_…` folder inside the
  zip — but this is UNVERIFIED on a clean Windows machine; see
  [build-and-run.md](build-and-run.md) for the honest caveat.

## Known limitation

The packaged binary has not been launch-smoked: the build host has no wine and
no Windows machine, so nobody has yet executed `LastAnimal.exe`. The export
gate proves the binary is a valid PE carrying the C# assembly; it does not
prove the game renders on your desktop. If it does not start, report the exact
error text — do not assume the zip is bad before checking SmartScreen (and
check that the `data_…` folder is still beside the .exe).

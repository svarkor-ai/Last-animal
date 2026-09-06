using Godot;

// Last Animal — M11 save-progression (MC 890.15, gunilla, 2026-09-06).
//
// ENGINE-SIDE seam that points the ISaveStore write path at `user://` (the
// OS user-data dir), NOT the repo / group-readable working tree. This is what
// makes the Phase-12 DoD true in production: the round-trip test proves the
// logic in SaveSystem.cs, and this class proves the save lands OUT of the
// shared tree. It needs GodotSharp, so it is compiled ONLY by the engine
// project (LastAnimalPreflight.csproj) — never by the headless test project.
namespace LastAnimal.Save;

/// <summary>
/// Godot storage for the save file. SavePath resolves to
/// `user://savegame.json` (globalized to the OS user-data directory).
/// </summary>
public class GodotSaveStore : ISaveStore
{
    public string SavePath => ProjectSettings.GlobalizePath("user://" + SaveSystem.SaveFileName);

    public void WriteAllText(string path, string contents) =>
        System.IO.File.WriteAllText(path, contents);

    public string ReadAllText(string path) =>
        System.IO.File.ReadAllText(path);

    public bool Exists(string path) => System.IO.File.Exists(path);
}

using System;
using System.Text.Json;

// Last Animal — M11 save-progression (MC 890.15, gunilla, 2026-09-06). Pure logic.
//
// C14 contract (PHASE0.md C14):
//   SaveSystem.Save(GameState), SaveSystem.Load() -> GameState? ; GameState.Version
//   schema-guarded. Persists DNA counters (M02), emotion (M03), progression, zone.
//
// Design (no Godot types, no window — I3; no cross-module refs — I4):
//   - This module is ENGINE-FREE. The write path is a seam, ISaveStore, so:
//       * production (engine) uses GodotSaveStore below, which writes to
//         `user://` — the OS user-data dir, NEVER the repo / group-readable
//         tree (DoD: "no secrets land in the group-readable tree");
//       * the headless `dotnet test` uses TempDirSaveStore (a System.IO dir
//         under the OS temp dir), so the round-trip DoD runs without a window.
//   - Serialization is System.Text.Json: Save serializes GameState to one
//     JSON file; Load parses it back. Nothing here touches the EventBus or
//     any other module (I4).
//   - Versioned schema guard: Load compares the saved GameState.Version to
//     CurrentVersion. On a mismatch the save is REJECTED (Load returns null)
//     and a human-readable upgrade path is captured on `Log`, exactly per the
//     Phase-12 DoD ("versioned schema rejects an out-of-date save with a
//     logged upgrade path").
namespace LastAnimal.Save;

/// <summary>
/// Storage seam for the save file. The engine impl (GodotSaveStore) resolves
/// to `user://savegame.json`; a headless test impl resolves to a temp dir.
/// </summary>
public interface ISaveStore
{
    /// <summary>Full path the save file is written to / read from.</summary>
    string SavePath { get; }

    void WriteAllText(string path, string contents);
    string ReadAllText(string path);
    bool Exists(string path);
}

/// <summary>
/// C14 persistence wiring. Static facades Save/Load match the contract
/// exactly; the serialization + schema guard are pure and testable headless.
/// </summary>
public static class SaveSystem
{
    /// <summary>Current schema version. Bump + add an upgrade path when fields change.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Name of the save file inside the store path.</summary>
    public const string SaveFileName = "savegame.json";

    /// <summary>
    /// Schema-guard / upgrade log. Append-only human-readable notes; Load
    /// writes an upgrade path here when it rejects an out-of-date save.
    /// </summary>
    public static readonly System.Collections.Generic.List<string> Log = new();

    /// <summary>Serialize and persist a GameState (C14 Save). Returns false on any I/O error.</summary>
    public static bool Save(GameState state, ISaveStore store)
    {
        if (state == null) { Log.Add("SaveSystem: refused to save a null GameState."); return false; }
        if (store == null) { Log.Add("SaveSystem: refused to save with a null store."); return false; }

        // Always stamp the current schema version so a save never claims an
        // older/newer Version than this build understands.
        state.Version = CurrentVersion;

        string json;
        try
        {
            json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            Log.Add($"SaveSystem: serialization failed: {ex.Message}");
            return false;
        }

        try
        {
            store.WriteAllText(store.SavePath, json);
            Log.Add($"SaveSystem: saved GameState (v{CurrentVersion}) to {store.SavePath}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Add($"SaveSystem: write failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load a GameState (C14 Load -&gt; GameState?). Returns the state on a
    /// version match; returns null (rejecting the save) when the file is
    /// absent or the version does not match, writing an upgrade path to Log.
    /// </summary>
    public static GameState? Load(ISaveStore store)
    {
        if (store == null) { Log.Add("SaveSystem: Load called with null store."); return null; }

        if (!store.Exists(store.SavePath))
        {
            Log.Add($"SaveSystem: no save found at {store.SavePath} — treating as first run.");
            return null;
        }

        string json;
        try
        {
            json = store.ReadAllText(store.SavePath);
        }
        catch (Exception ex)
        {
            Log.Add($"SaveSystem: read failed: {ex.Message}");
            return null;
        }

        GameState? state;
        try
        {
            state = JsonSerializer.Deserialize<GameState>(json);
        }
        catch (Exception ex)
        {
            Log.Add($"SaveSystem: parse failed (corrupt save): {ex.Message}. Upgrade path: delete the save or restore a valid one.");
            return null;
        }

        if (state == null)
        {
            Log.Add("SaveSystem: save parsed to null (empty file). Upgrade path: re-create a fresh save.");
            return null;
        }

        // ---- Versioned schema guard (DoD). ----
        if (state.Version < CurrentVersion)
        {
            Log.Add($"SaveSystem: REJECTED out-of-date save (found v{state.Version}, current v{CurrentVersion}). " +
                    "Upgrade path: start a new game on the current schema, or run the migration " +
                    $"m11_upgrade_v{state.Version}_to_v{CurrentVersion} which fills new fields from defaults.");
            return null;
        }
        if (state.Version > CurrentVersion)
        {
            Log.Add($"SaveSystem: REJECTED save from a newer build (found v{state.Version}, current v{CurrentVersion}). " +
                    "Upgrade path: update the game, or this save cannot be read until the schema catches up.");
            return null;
        }

        Log.Add($"SaveSystem: loaded GameState (v{state.Version}) from {store.SavePath}");
        return state;
    }

    /// <summary>
    /// M11-equivalent of the standard two-sided calibration: a broken save
    /// must be rejected. Rewrites the on-disk file's Version header without
    /// the real schema. Returns false when nothing to repair.
    /// </summary>
    public static bool RewriteSavedVersionForTest(ISaveStore store, int badVersion)
    {
        if (!store.Exists(store.SavePath)) return false;
        var json = store.ReadAllText(store.SavePath);
        var patched = System.Text.RegularExpressions.Regex.Replace(
            json, "\"Version\"\\s*:\\s*\\d+", $"\"Version\": {badVersion}");
        store.WriteAllText(store.SavePath, patched);
        return true;
    }
}

/// <summary>
/// Headless / test storage: a plain System.IO directory under the OS temp
/// dir, so `dotnet test` exercises the exact same round-trip without a
/// window and without writing into the repo tree.
/// </summary>
public class TempDirSaveStore : ISaveStore
{
    public string SavePath { get; }

    public TempDirSaveStore(string? dir = null)
    {
        var d = dir ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lastanimal_save_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(d);
        SavePath = System.IO.Path.Combine(d, SaveSystem.SaveFileName);
    }

    public void WriteAllText(string path, string contents) =>
        System.IO.File.WriteAllText(path, contents);

    public string ReadAllText(string path) =>
        System.IO.File.ReadAllText(path);

    public bool Exists(string path) => System.IO.File.Exists(path);
}

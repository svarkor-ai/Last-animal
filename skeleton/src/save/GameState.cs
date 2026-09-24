using System;
using System.Collections.Generic;

// Last Animal — M11 save-progression (MC 890.15, gunilla, 2026-09-06).
//
// C14 GameState schema. Pure data: no Godot types, no window — runs headless
// under `dotnet test` (I3). Serialized by SaveSystem via System.Text.Json.
//
// Design (mirrors the milestone module conventions — one concern per file):
//   - This file is ONLY the data model. Persistence logic lives in SaveSystem.cs.
//   - `Version` is the schema-guard key: SaveSystem.CurrentVersion must equal
//     GameState.Version for a save to load; older saves are rejected with a
//     logged upgrade path (restored as a fresh version), never silently read.
//   - It carries the M02 DNA counters, M03 companion emotion, general
//     progression, and the M07 zone id — per C14.
//
// Non-mutating public setters keep System.Text.Json round-trips trivial: the
// serializer writes/reads the same property names, so a representative state
// comes back byte-for-byte equal via { get; set; }.
namespace LastAnimal.Save;

/// <summary>
/// Serializable snapshot of player progression (C14).
/// </summary>
public class GameState
{
    /// <summary>Schema version. Must equal SaveSystem.CurrentVersion to load.</summary>
    public int Version { get; set; } = SaveSystem.CurrentVersion;

    /// <summary>Current zone id (M07 zone scene key, e.g. "meadow").</summary>
    public string ZoneId { get; set; } = "meadow";

    /// <summary>General progression counter (chapters cleared / story beacons).</summary>
    public int Progression { get; set; } = 0;

    /// <summary>
    /// Learned DNA counters (M02): the nucleotide counter values the player has
    /// unlocked/spoken as the ecosystem's language. Values are 0-3 (A,C,G,T).
    /// Persisted so progression survives a restart (C14 "persists DNA counters").
    /// </summary>
    public List<int> LearnedDnaCounters { get; set; } = new();

    /// <summary>
    /// HUD DNA-meter event count at save time (MC 1344): how many
    /// DnaExtracted/DnaSpoken events the meter had counted. Persisted so the
    /// meter itself (not just the learned counters) survives a load.
    /// </summary>
    public int DnaEventCount { get; set; } = 0;

    /// <summary>
    /// Player health at save time (MC 1348 N1). Persisted so the F9 load path
    /// can rescue a dead player: the restored value clears the model's
    /// IsDead and movement works again. Old saves without the field
    /// deserialize at the 100 default (a generous restore, not a softlock).
    /// </summary>
    public int PlayerHealth { get; set; } = 100;

    /// <summary>Companion entity id (M03; -1 = no companion).</summary>
    public int CompanionEntityId { get; set; } = -1;

    /// <summary>Companion loyalty 0-100 (M03 emotion state source).</summary>
    public int CompanionLoyalty { get; set; } = 50;

    /// <summary>
    /// Derived hidden emotion state label (M03 C8): Content|Neutral|Anxious|Betrayed.
    /// Recomputed on load from CompanionLoyalty by the caller; stored here as the
    /// C14 "persists emotion" snapshot.
    /// </summary>
    public string EmotionState { get; set; } = "Neutral";

    /// <summary>Representative game state used by the round-trip DoD test.</summary>
    public static GameState Representative()
    {
        return new GameState
        {
            Version = SaveSystem.CurrentVersion,
            ZoneId = "canyon",
            Progression = 3,
            LearnedDnaCounters = new List<int> { 0, 2, 1, 3 },
            DnaEventCount = 12,
            CompanionEntityId = 7,
            CompanionLoyalty = 84,
            PlayerHealth = 100,
            EmotionState = "Content"
        };
    }
}

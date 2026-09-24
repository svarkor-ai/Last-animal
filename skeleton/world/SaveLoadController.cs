using LastAnimal.Dna;
using LastAnimal.Ecosystem;
using LastAnimal.Npc;
using LastAnimal.Save;
using LastAnimal.Ui;
using System;
using System.Collections.Generic;

// Last Animal — save/load production wiring (MC 1344, DA findings 1+2).
//
// SaveLoadController: the save/load concern extracted from WorldDirector so
// the composition root stays under the file-size ceiling and this file owns
// ONE concern: snapshot the live game state -> GameState, and apply a loaded
// GameState back onto the live game.
//
// What load actually restores (the DA findings this fixes):
//   - the spoken-DNA history (_spokenDna), rebuilt from the saved
//     LearnedDnaCounters as ONE signature whose per-position most-common
//     nucleotides ARE the counters — so a save->load->save round-trip is
//     stable and EcosystemAdaptation.ModelPlayerDna sees the same profile;
//   - the HUD DNA meter (DnaEventCount) via Hud.UpdateDnaMeter — the bus only
//     increments, so a restore cannot ride a bus event;
//   - Progression (no hardcoded zeros — the counter lives here and ticks on
//     first entry into each new zone);
//   - the companion entity id + loyalty;
//   - the saved zone: the director's enter-zone callback is invoked so the
//     zone's SpawnSet is re-applied (re-entry, not just a field write).
//
// Engine-free apart from the GodotSaveStore seam (same as WorldDirector's
// previous inline implementation): pure logic + SaveSystem.
namespace LastAnimal.World;

/// <summary>
/// Owns SaveGame/LoadGame for the playable path: builds the GameState
/// snapshot from live director state and applies a loaded snapshot back.
/// </summary>
public sealed class SaveLoadController
{
    private readonly List<LanguageSignature> _spokenDna;
    private readonly CompanionComponent _companion;
    private readonly Hud _hud;
    private readonly Func<string> _currentZone;
    private readonly Action<string> _enterZone;

    // Progression: chapters cleared = first entries into DISTINCT zones
    // (MC 1348 N2 — the counting rule lives in the pure ZoneProgression so it
    // is headless-testable; this controller only delegates). Seeded with the
    // default zone so the boot-time meadow entry is not counted as progress.
    private readonly ZoneProgression _progression = new(EcosystemSpawner.DefaultZone);

    // Player health seam (MC 1348 N1): read at save, restored on load, so the
    // F9 load path rescues a dead player.
    private readonly Func<int> _playerHealth;
    private readonly Action<int> _restoreHealth;

    public SaveLoadController(
        List<LanguageSignature> spokenDna,
        CompanionComponent companion,
        Hud hud,
        Func<string> currentZone,
        Action<string> enterZone,
        Func<int> playerHealth,
        Action<int> restoreHealth)
    {
        _spokenDna = spokenDna;
        _companion = companion;
        _hud = hud;
        _currentZone = currentZone;
        _enterZone = enterZone;
        _playerHealth = playerHealth;
        _restoreHealth = restoreHealth;
    }

    /// <summary>Progression counter (chapters cleared / distinct zones entered).</summary>
    public int Progression => _progression.Chapters;

    /// <summary>
    /// Progression tick from the director's zone-entered handler: the FIRST
    /// entry into each distinct zone clears a chapter; re-entering a zone
    /// already visited (including a load's re-entry) does not.
    /// </summary>
    public void OnZoneEntered(string zoneId) => _progression.OnZoneEntered(zoneId);

    /// <summary>Snapshot the live game into user://savegame.json. False on I/O error.</summary>
    public bool Save()
    {
        var state = new GameState
        {
            ZoneId = _currentZone(),
            Progression = _progression.Chapters,
            // The per-position most-common nucleotide of the spoken history
            // (the C14 "persists DNA counters" snapshot).
            LearnedDnaCounters = BuildLearnedCounters(),
            DnaEventCount = _hud.DnaMeter,
            CompanionEntityId = _companion.CompanionEntityId,
            CompanionLoyalty = _companion.Loyalty,
            // MC 1348 N1: health is part of the snapshot so a load can rescue
            // a dead player.
            PlayerHealth = _playerHealth(),
        };
        return SaveSystem.Save(state, new GodotSaveStore());
    }

    /// <summary>
    /// Apply the saved state back onto the live game and re-enter the saved
    /// zone. False when there is no loadable save (schema guard / no file).
    /// </summary>
    public bool Load()
    {
        var loaded = SaveSystem.Load(new GodotSaveStore());
        if (loaded == null) return false;

        RestoreDna(loaded.LearnedDnaCounters);
        _companion.CompanionEntityId = loaded.CompanionEntityId;
        _companion.Loyalty = loaded.CompanionLoyalty;
        _hud.UpdateDnaMeter(loaded.DnaEventCount);
        // MC 1348 N1: restore health (reviving a dead player) and mirror it
        // on the HUD life gauge, which the bus does not drive.
        _restoreHealth(loaded.PlayerHealth);
        _hud.UpdateLife(loaded.PlayerHealth);
        _progression.SeedForLoad(loaded.ZoneId, loaded.Progression);
        // Re-enter: the director's handler re-applies the zone's SpawnSet.
        _enterZone(loaded.ZoneId);
        return true;
    }

    /// <summary>Rebuild the spoken history from the saved counters: ONE
    /// signature whose nucleotides are the counters, so a subsequent save
    /// reproduces the same LearnedDnaCounters (stable round-trip).</summary>
    private void RestoreDna(List<int> counters)
    {
        _spokenDna.Clear();
        if (counters.Count > 0)
            _spokenDna.Add(new LanguageSignature(counters.ToArray()));
    }

    /// <summary>Per-position most-common nucleotide across the spoken history
    /// (the C14 LearnedDnaCounters snapshot; empty when nothing was spoken).</summary>
    private List<int> BuildLearnedCounters()
    {
        var counters = new List<int>();
        if (_spokenDna.Count == 0) return counters;
        int len = 0;
        foreach (var s in _spokenDna) len = Math.Max(len, s.Nucleotides.Length);
        for (int i = 0; i < len; i++)
        {
            var tally = new int[4];
            foreach (var s in _spokenDna)
                if (i < s.Nucleotides.Length) tally[s.Nucleotides[i]]++;
            int best = 0;
            for (int n = 1; n < 4; n++) if (tally[n] > tally[best]) best = n;
            counters.Add(best);
        }
        return counters;
    }
}

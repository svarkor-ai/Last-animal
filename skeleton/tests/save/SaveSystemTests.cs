using System;
using System.Collections.Generic;
using Xunit;
using LastAnimal.Save;

// Last Animal — M11 save-progression tests (MC 890.15, gunilla, 2026-09-06).
//
// C14 contract tests + the Phase-12 DoD round-trip. All tests run headless:
// SaveSystem.Load/Save are pure logic (I3); the store is the engine-free
// TempDirSaveStore (System.IO under the OS temp dir), so nothing touches the
// repo / group-readable working tree. Each test gets a fresh temp store in a
// brand-new directory, so the suite is order-independent and repeatable.
namespace LastAnimal.Tests.Save;

public class SaveSystemTests
{
    private TempDirSaveStore NewStore() => new TempDirSaveStore();

    // ------------------------------------------------------------------
    // C14: Save -> Load round-trip preserves a representative GameState
    // ------------------------------------------------------------------

    [Fact]
    public void RoundTrip_PreservesRepresentativeGameState()
    {
        var store = NewStore();
        var original = GameState.Representative();

        bool saved = SaveSystem.Save(original, store);
        Assert.True(saved, "Save should succeed to a temp store");

        var loaded = SaveSystem.Load(store);
        Assert.NotNull(loaded);
        Assert.Equal(original.ZoneId, loaded.ZoneId);
        Assert.Equal(original.Progression, loaded.Progression);
        Assert.Equal(original.CompanionEntityId, loaded.CompanionEntityId);
        Assert.Equal(original.CompanionLoyalty, loaded.CompanionLoyalty);
        Assert.Equal(original.EmotionState, loaded.EmotionState);
        Assert.Equal(original.Version, loaded.Version);
        Assert.Equal(original.LearnedDnaCounters, loaded.LearnedDnaCounters);
        Assert.Equal(original.DnaEventCount, loaded.DnaEventCount);
        Assert.Equal(original.PlayerHealth, loaded.PlayerHealth);
    }

    [Fact]
    public void RoundTrip_WritesOutsideRepoTree()
    {
        // The save file must live in the OS temp dir (or user:// in the engine),
        // NOT the shared /srv/workspace tree. Prove the store path is under the
        // temp dir and the file exists there.
        var store = NewStore();
        Assert.True(SaveSystem.Save(GameState.Representative(), store));
        Assert.True(System.IO.File.Exists(store.SavePath));
        Assert.StartsWith(System.IO.Path.GetTempPath(), store.SavePath);
    }

    [Fact]
    public void Save_StampsCurrentVersion()
    {
        var store = NewStore();
        var state = GameState.Representative();
        state.Version = 999; // deliberately wrong
        SaveSystem.Save(state, store);
        Assert.Equal(SaveSystem.CurrentVersion, state.Version);
    }

    [Fact]
    public void PersistsDnaCounters_Emotion_Progression_Zone()
    {
        // C14: persists DNA counters (M02), emotion (M03), progression, zone.
        var store = NewStore();
        var state = new GameState
        {
            LearnedDnaCounters = new List<int> { 3, 0, 1, 3, 2 },
            EmotionState = "Betrayed",
            CompanionLoyalty = 12,
            Progression = 5,
            ZoneId = "ruins"
        };
        SaveSystem.Save(state, store);
        var loaded = SaveSystem.Load(store);
        Assert.NotNull(loaded);
        Assert.Equal(new List<int> { 3, 0, 1, 3, 2 }, loaded.LearnedDnaCounters);
        Assert.Equal("Betrayed", loaded.EmotionState);
        Assert.Equal(12, loaded.CompanionLoyalty);
        Assert.Equal(5, loaded.Progression);
        Assert.Equal("ruins", loaded.ZoneId);
    }

    [Fact]
    public void RoundTrip_PreservesPlayerHealth()
    {
        // MC 1348 N1: the save must capture player health so a load rescues a
        // dead player (F9 from the death state restores a live HP value).
        var store = NewStore();
        var original = GameState.Representative();
        original.PlayerHealth = 42;

        Assert.True(SaveSystem.Save(original, store));
        var loaded = SaveSystem.Load(store);
        Assert.NotNull(loaded);
        Assert.Equal(42, loaded.PlayerHealth);
    }

    // ------------------------------------------------------------------
    // C14: versioned schema guard
    // ------------------------------------------------------------------

    [Fact]
    public void Load_RejectsOutOfDateSave_AndLogsUpgradePath()
    {
        var store = NewStore();
        SaveSystem.Save(GameState.Representative(), store);
        // Rewrite the on-disk Version header to an older schema.
        Assert.True(SaveSystem.RewriteSavedVersionForTest(store, SaveSystem.CurrentVersion - 1));

        SaveSystem.Log.Clear();
        var loaded = SaveSystem.Load(store);
        Assert.Null(loaded); // outdated save rejected

        var logged = string.Join(" | ", SaveSystem.Log);
        Assert.Contains("REJECTED", logged);
        Assert.Contains("Upgrade path", logged);
        Assert.Contains("m11_upgrade", logged);
    }

    [Fact]
    public void Load_RejectsNewerThanCurrent_AndLogsUpgradePath()
    {
        var store = NewStore();
        SaveSystem.Save(GameState.Representative(), store);
        SaveSystem.RewriteSavedVersionForTest(store, SaveSystem.CurrentVersion + 1);

        SaveSystem.Log.Clear();
        var loaded = SaveSystem.Load(store);
        Assert.Null(loaded);
        Assert.Contains("newer build", string.Join(" | ", SaveSystem.Log));
    }

    [Fact]
    public void Load_ReturnsNullAndWelcomesFirstRun_WhenNoSaveExists()
    {
        var store = NewStore(); // nothing saved yet
        SaveSystem.Log.Clear();
        var loaded = SaveSystem.Load(store);
        Assert.Null(loaded);
        Assert.Contains("first run", string.Join(" | ", SaveSystem.Log));
    }

    [Fact]
    public void Save_RejectsNullState()
    {
        var store = NewStore();
        Assert.False(SaveSystem.Save(null!, store));
    }
}

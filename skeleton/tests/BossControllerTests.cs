using LastAnimal.Dna;
using LastAnimal.Ecosystem;
using Xunit;

// Last Animal — C15 BossController.Phase test suite (MC 1344 DA finding:
// C15 BossController.Phase was MISSING — SpawnedEnemy.IsBoss existed but no
// phase controller). Pure-logic coverage, engine-free (I3).
//
// Contract (PHASE0.md §4 C15): BossController.Phase(state, dnaRead) —
// adaptation toggles hard counters. A phase transition must be derivable
// purely from the observed spoken-DNA history, and the counter multiplier
// the phase toggles must grow monotonically so the director can re-field a
// meaner SpawnSet when the phase steps up.
namespace LastAnimal.Tests;

public class BossControllerTests
{
    [Fact]
    public void Phase_NoDnaRead_StaysInPhaseOne()
    {
        var next = BossController.Phase(BossController.Initial, dnaRead: null);
        Assert.Equal(1, next.Phase);
        Assert.False(next.Changed);
    }

    [Fact]
    public void Phase_BelowFirstStep_NoTransition()
    {
        // 2 observed < SignaturesPerPhase(3): the boss has not learned enough
        // to counter — the phase must not move.
        var next = BossController.Phase(BossController.Initial, NewProfile(observed: 2));
        Assert.Equal(1, next.Phase);
        Assert.False(next.Changed);
    }

    [Fact]
    public void Phase_CrossingStep_Transitions_AndFlagsChanged()
    {
        var next = BossController.Phase(BossController.Initial, NewProfile(observed: 3));
        Assert.Equal(2, next.Phase);
        Assert.True(next.Changed);
    }

    [Fact]
    public void Phase_IsMonotonic_NeverStepsDown()
    {
        // A boss already in phase 2 stays there when the profile stops growing.
        var atTwo = BossController.Phase(BossController.Initial, NewProfile(observed: 3));
        var again = BossController.Phase(atTwo, NewProfile(observed: 3));
        Assert.Equal(2, again.Phase);
        Assert.False(again.Changed);
    }

    [Fact]
    public void Phase_CapsAtMaxPhase()
    {
        var next = BossController.Phase(BossController.Initial, NewProfile(observed: 99));
        Assert.Equal(BossController.MaxPhase, next.Phase);
    }

    [Fact]
    public void CounterMultiplier_GrowsWithPhase_AndIsIdentityAtPhaseOne()
    {
        Assert.Equal(1.0f, BossController.CounterMultiplier(1), 3);
        Assert.True(BossController.CounterMultiplier(2) > BossController.CounterMultiplier(1));
        Assert.True(BossController.CounterMultiplier(3) > BossController.CounterMultiplier(2));
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static CounterProfile NewProfile(int observed)
    {
        var sigs = new LanguageSignature[observed];
        for (int i = 0; i < observed; i++)
            sigs[i] = new LanguageSignature(new[] { 0, 1, 2, 3 }, id: 1);
        return EcosystemAdaptation.ModelPlayerDna(sigs);
    }
}

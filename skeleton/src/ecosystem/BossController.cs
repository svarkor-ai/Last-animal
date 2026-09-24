using LastAnimal.Dna;

// Last Animal — C15 BossController (MC 1344 DA finding: the class was
// MISSING — SpawnedEnemy.IsBoss existed but nothing phased the boss).
//
// Contract (PHASE0.md §4 C15): `BossController.Phase(state, dnaRead)` —
// adaptation toggles hard counters. The phase is a pure function of the
// boss's current phase state and the player's spoken-DNA CounterProfile:
// every SignaturesPerPhase observed signatures step the boss up one phase
// (capped at MaxPhase), and each phase raises the hard-counter multiplier
// the director applies when it re-fields the zone's SpawnSet.
//
// Design (pure logic, no Godot types — I3): the controller returns an
// immutable BossPhaseState with a Changed flag; it does NOT touch the bus
// or the scene. The C2 EcosystemAdapted emit and the SpawnSet re-application
// are the caller's (WorldDirector's) job — same split as EcosystemSpawner.
namespace LastAnimal.Ecosystem;

/// <summary>Immutable phase state of the zone boss (C15).</summary>
public readonly struct BossPhaseState
{
    /// <summary>1-based phase; 1 = base counters, MaxPhase = fully countered.</summary>
    public int Phase { get; }

    /// <summary>True only on the tick where the phase stepped up.</summary>
    public bool Changed { get; }

    public BossPhaseState(int phase, bool changed)
    {
        Phase = phase;
        Changed = changed;
    }
}

/// <summary>
/// Boss phase controller (C15). Drives the boss's hard-counter escalation
/// from the player's spoken-DNA history. Pure logic.
/// </summary>
public static class BossController
{
    /// <summary>Observed signatures needed per phase step.</summary>
    public const int SignaturesPerPhase = 3;

    /// <summary>Highest phase the escalation reaches.</summary>
    public const int MaxPhase = 3;

    /// <summary>The phase a freshly spawned boss starts in.</summary>
    public static BossPhaseState Initial => new BossPhaseState(1, changed: false);

    /// <summary>
    /// C15 entry point. Evaluate the boss's phase against the player's
    /// spoken-DNA profile. Returns the (possibly advanced) state; the Changed
    /// flag is true only on the transition tick.
    /// </summary>
    public static BossPhaseState Phase(BossPhaseState state, CounterProfile? dnaRead)
    {
        int observed = dnaRead?.ObservedCount ?? 0;
        int target = System.Math.Min(MaxPhase, 1 + observed / SignaturesPerPhase);
        return new BossPhaseState(target, target != state.Phase);
    }

    /// <summary>Hard-counter multiplier for a phase: 1.0 at phase 1, growing
    /// 0.25 per step — the lever the director applies to re-fielded spawns.</summary>
    public static float CounterMultiplier(int phase)
        => 1f + 0.25f * (System.Math.Max(1, phase) - 1);
}

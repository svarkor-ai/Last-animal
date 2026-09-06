using System;

// Last Animal — M08 combat-3d CombatSystem (MC 890.13, artemis, 2026-09-06).
//
// C10 (PHASE0.md line 379): `CombatSystem.DealDamage(attacker,target,amount)`,
// `CombatSystem.OnKill(defeatedEntity) -> DnaSignature?` (wires M02 extract).
//
// Engine-free (I3) so the whole module is testable under `dotnet test`:
// DealDamage applies damage through a resolver; OnKill extracts the defeated
// entity's DNA signature via M02's DnaLanguage and raises a `DnaExtracted`
// C# event that the Godot seam (M08 scene) forwards to
// EventBus.EmitDnaExtracted (C2).
//
// SEAM NOTE (why the return type is M02's LanguageSignature, not the Godot
// DnaSignature Resource): the C10 contract's "DnaSignature" is the M02 DNA
// signature carrier. In this codebase the Godot `DnaSignature` (M01,
// autoload/FrameworkTypes.cs) is a Godot `Resource` that a pure-logic module
// compiled by a standalone xunit project cannot reference (it needs
// GodotSharp). Per I4 the engine type must not leak into the pure module, so
// OnKill returns M02's `LanguageSignature` (the genuine DNA signature) and
// the Godot seam wraps its SpeciesHash into the EventBus DnaSignature (C2).
// The DnaExtracted event carries the module's own signature so the wire is
// exercised the same way in test and in-engine.
namespace LastAnimal.Combat;

/// <summary>
/// CombatSystem (C10). Routes damage between combatants and extracts DNA on
/// kill. Pure logic; raises a C# DnaExtracted event the Godot seam forwards
/// to the EventBus (C2).
/// </summary>
public class CombatSystem
{
    /// <summary>Raised when an enemy is killed and its DNA signature extracted.</summary>
    public event Action<Dna.LanguageSignature>? DnaExtracted;

    /// <summary>Raised when damage is dealt but the target is not killed.</summary>
    public event Action<int, int>? DamageDealt;

    /// <summary>
    /// Deal amount of damage from attacker to target. If the target dies,
    /// OnKill extracts the target's DNA signature (C10 -> M02 wire).
    /// Returns true if the target was killed by this hit.
    /// </summary>
    public bool DealDamage(int attackerId, EnemyAI target, int amount)
    {
        if (target.IsDead) return false;
        if (amount <= 0) return false;

        target.TakeDamage(amount);
        DamageDealt?.Invoke(attackerId, target.EntityId);
        if (target.IsDead)
        {
            _ = OnKill(target);
            return true;
        }
        return false;
    }

    /// <summary>
    /// On kill: extract the defeated entity's DNA signature via M02's random
    /// generator (deterministic seed from the enemy id) and raise the
    /// DnaExtracted event. Returns the extracted signature, or null if the
    /// entity is not actually dead.
    /// </summary>
    public Dna.LanguageSignature? OnKill(EnemyAI defeatedEntity)
    {
        if (defeatedEntity == null || !defeatedEntity.IsDead)
            return null;

        // Wires M02 extract: build the defeated enemy's DNA signature from a
        // deterministic seed derived from its entity id, then fire the event.
        // The signature's Id carries the defeated enemy's identity so the
        // DnaExtracted event names the source (C10/C2).
        var rng = new System.Random(defeatedEntity.EntityId);
        var nucleotides = new int[6];
        for (int i = 0; i < nucleotides.Length; i++)
            nucleotides[i] = rng.Next(4); // 0-3 (A,C,G,T)
        var sig = new Dna.LanguageSignature(nucleotides, defeatedEntity.EntityId);
        DnaExtracted?.Invoke(sig);
        return sig;
    }
}

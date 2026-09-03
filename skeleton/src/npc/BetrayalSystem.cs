using System;

// Last Animal — M03 npc-emotion (MC 890.2, dobbie, 2026-09-03).
//
// Port of /srv/workspace/animal/src/Animal.Gameplay/BetrayalSystem.cs (C7).
// Pure logic: no Godot types, no window — runs headless under `dotnet test` (I3).
//
// Port notes (semantics preserved 1:1 from the prior art):
//   - BaseBetrayalDamage=10 (same constant).
//   - CheckBetrayal: loyalty <= 0 AND HasCompanion (same guard as the source).
//   - ExecuteBetrayal: breaks the companion bond, returns a BetrayalResult
//     with damage = BaseBetrayalDamage + (int)ProximityRadius (same formula).
//   - BetrayalResult carries BetrayerEntityId / BetrayedTargetId /
//     DamageDealt / LoyaltyBeforeBetrayal (same fields as the source).
//
// The prior art's `Entity` (Animal.Core) is a component registry; this port
// takes the CompanionComponent directly so the module stays engine-free. The
// null-guard semantics are preserved: a null component == no companion.
namespace LastAnimal.Npc;

/// <summary>
/// Result of executing a betrayal, containing details about what happened.
/// </summary>
public class BetrayalResult
{
    /// <summary>The entity ID of the betrayer (the companion that betrayed).</summary>
    public int BetrayerEntityId { get; set; }

    /// <summary>The entity ID of the target that was betrayed.</summary>
    public int BetrayedTargetId { get; set; }

    /// <summary>Damage dealt to the betrayed target.</summary>
    public int DamageDealt { get; set; }

    /// <summary>
    /// Previous loyalty level before betrayal (always 0 when betrayal triggers,
    /// but useful for event logging).
    /// </summary>
    public int LoyaltyBeforeBetrayal { get; set; }
}

/// <summary>
/// Betrayal system for companion relationships (C7 port).
/// When companion loyalty reaches zero, the companion may betray its owner.
/// Betrayal breaks the companion bond and deals damage to the owner.
/// </summary>
public class BetrayalSystem
{
    /// <summary>Base betrayal damage.</summary>
    private const int BaseBetrayalDamage = 10;

    /// <summary>
    /// Check if an entity is in a betrayal state.
    /// Betrayal occurs when loyalty is zero AND the entity has an active companion.
    /// </summary>
    public bool CheckBetrayal(CompanionComponent? comp)
    {
        if (comp == null) return false;
        if (!comp.HasCompanion) return false;

        return comp.Loyalty <= 0;
    }

    /// <summary>
    /// Execute betrayal: break the companion bond and calculate damage.
    /// Returns null if the entity has no companion component or no active companion.
    /// </summary>
    public BetrayalResult? ExecuteBetrayal(CompanionComponent? comp)
    {
        if (comp == null) return null;
        if (!comp.HasCompanion) return null;

        int betrayedTarget = comp.CompanionEntityId;
        int previousLoyalty = comp.Loyalty;

        // Calculate betrayal damage — companions with higher historical loyalty
        // deal more damage (they knew the owner better, so betrayal is worse)
        // For now, use base damage; a full system would track loyalty history.
        int damage = CalculateBetrayalDamage(comp);

        // Break the companion bond
        comp.BreakCompanion();

        return new BetrayalResult
        {
            BetrayerEntityId = comp.Id,
            BetrayedTargetId = betrayedTarget,
            DamageDealt = damage,
            LoyaltyBeforeBetrayal = previousLoyalty
        };
    }

    /// <summary>
    /// Calculate betrayal damage based on companion state.
    /// Higher base damage for established relationships.
    /// </summary>
    private static int CalculateBetrayalDamage(CompanionComponent comp)
    {
        // Companions with larger proximity radius had closer relationships
        // so betrayal is more damaging. Scale: base + proximity factor.
        return BaseBetrayalDamage + (int)(comp.ProximityRadius);
    }
}

using System;

// Last Animal — M03 npc-emotion (MC 890.2, dobbie, 2026-09-03).
//
// Port of /srv/workspace/animal/src/Animal.Entities/Components/CompanionComponent.cs.
// Pure data: no Godot types, no window — runs headless under `dotnet test` (I3).
//
// Port notes (semantics preserved 1:1 from the prior art):
//   - CompanionEntityId = -1 means no companion (same sentinel as the source).
//   - Loyalty is clamped to [0, 100] on every ModifyLoyalty (same clamp).
//   - SetCompanion clamps loyalty to [0, 100] (same as the source).
//   - BreakCompanion sets CompanionEntityId = -1 and Loyalty = 0 (same).
//   - LoyaltyPercent = Loyalty / 100f (same).
//   - The source's `Component` base (Animal.Core.IComponent) is dropped: this
//     port is engine-free and the component-registry seam is not part of the
//     C6/C7 contract surface.
//   - `Id` is added (not in the prior art) so the C7 BetrayalResult can carry
//     the entity identity the source read from Entity.Id.
namespace LastAnimal.Npc;

/// <summary>
/// Companion component for entities that can have companion relationships.
/// Tracks the companion link, loyalty (0-100), and proximity radius.
/// Used for the Animal Chain mechanic.
/// </summary>
public class CompanionComponent
{
    public int CompanionEntityId { get; set; } = -1; // -1 means no companion
    public int Loyalty { get; set; } = 50; // 0-100
    public float ProximityRadius { get; set; } = 5.0f;

    /// <summary>Entity identity carried alongside the companion (C7 port seam).</summary>
    public int Id { get; set; } = 0;

    public bool HasCompanion => CompanionEntityId >= 0;

    public void SetCompanion(int entityId)
    {
        CompanionEntityId = entityId;
        Loyalty = Math.Max(0, Math.Min(100, Loyalty));
    }

    public void BreakCompanion()
    {
        CompanionEntityId = -1;
        Loyalty = 0;
    }

    public void ModifyLoyalty(int delta)
    {
        Loyalty = Math.Max(0, Math.Min(100, Loyalty + delta));
    }

    public float LoyaltyPercent => Loyalty / 100f;
}

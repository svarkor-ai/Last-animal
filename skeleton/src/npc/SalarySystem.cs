using System;

// Last Animal — M03 npc-emotion (MC 890.2, dobbie, 2026-09-03).
//
// Port of /srv/workspace/animal/src/Animal.Gameplay/SalarySystem.cs (C6).
// Pure logic: no Godot types, no window — runs headless under `dotnet test` (I3).
//
// Port notes (semantics preserved 1:1 from the prior art):
//   - BaseSalary=1, PayBonus=5, SkipPenalty=3 (same constants).
//   - CalculateSalary = BaseSalary + (Loyalty / 10) (integer division, same
//     as the source); 0 when the entity has no companion component.
//   - PaySalary: ModifyLoyalty(+PayBonus), returns false when no companion.
//   - SkipSalary: ModifyLoyalty(-SkipPenalty), returns false when no companion.
//
// The prior art's `Entity` (Animal.Core) is a component registry; this port
// takes the CompanionComponent directly so the module stays engine-free. The
// null-guard semantics are preserved: a null component == no companion.
namespace LastAnimal.Npc;

/// <summary>
/// Salary system for companion loyalty management (C6 port).
/// Companions demand periodic salary payments. Paying increases loyalty,
/// skipping decreases it. Salary amount scales with current loyalty level.
/// </summary>
public class SalarySystem
{
    /// <summary>Base salary amount (minimum guaranteed).</summary>
    private const int BaseSalary = 1;

    /// <summary>Loyalty boost when salary is paid on time.</summary>
    private const int PayBonus = 5;

    /// <summary>Loyalty penalty when salary is skipped.</summary>
    private const int SkipPenalty = 3;

    /// <summary>
    /// Calculate the salary amount an entity's companion demands.
    /// Higher loyalty = higher salary demand (companions who trust more expect more).
    /// Returns 0 if the entity has no companion component.
    /// </summary>
    public int CalculateSalary(CompanionComponent? comp)
    {
        if (comp == null) return 0;

        // Salary scales: base + (loyalty / 10) so range is roughly 1-11
        return BaseSalary + (comp.Loyalty / 10);
    }

    /// <summary>
    /// Pay salary to the entity's companion, increasing loyalty.
    /// Returns false if the entity has no companion component.
    /// </summary>
    public bool PaySalary(CompanionComponent? comp)
    {
        if (comp == null) return false;

        int before = comp.Loyalty;
        comp.ModifyLoyalty(PayBonus);

        return true;
    }

    /// <summary>
    /// Skip paying salary, decreasing loyalty.
    /// Repeated skips will eventually drain loyalty to zero,
    /// triggering betrayal conditions.
    /// Returns false if the entity has no companion component.
    /// </summary>
    public bool SkipSalary(CompanionComponent? comp)
    {
        if (comp == null) return false;

        comp.ModifyLoyalty(-SkipPenalty);
        return true;
    }
}
